using foto4.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace foto4.Controllers
{
    [Route("Prodocts")]
    public class ProdoctsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProdoctsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: /Prodocts
        [HttpGet("")]
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            await AddBuiltInProductsIfMissingAsync();

            var products = await _context.Prodocts
                .Include(p => p.Category)
                .AsNoTracking()
                .ToListAsync();

            return View(products);
        }

        // GET: /Prodocts/5
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var prodoct = await _context.Prodocts
                .Include(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prodoct == null)
            {
                return NotFound();
            }

            return View(prodoct);
        }

        // GET: /Prodocts/SubmitRequest/5
        [HttpGet("SubmitRequest/{id:int}")]
        [Authorize]
        public async Task<IActionResult> SubmitRequest(int id)
        {
            var prodoct = await _context.Prodocts
                .Include(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prodoct == null)
            {
                return NotFound();
            }

            return View(prodoct);
        }

        // POST: /Prodocts/SubmitRequest/5
        [HttpPost("SubmitRequest/{id:int}")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> SubmitRequest(
            int id,
            string ClientName,
            string Contact,
            string Parameters,
            string Staus,
            List<IFormFile> UploadedFiles)
        {
            var prodoct = await _context.Prodocts
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prodoct == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(ClientName) || string.IsNullOrWhiteSpace(Contact))
            {
                TempData["ErrorMessage"] = "Попълнете име и телефон/имейл.";
                return View(prodoct);
            }

            var clientId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Challenge();
            }

            var serviceId = await GetServiceIdForPhotoProductAsync();

            if (serviceId == null)
            {
                TempData["ErrorMessage"] = "Няма създадена услуга в базата. Създай поне една услуга, за да може заявката да се запише в Orders.";
                return View(prodoct);
            }

            var uploadedFilePaths = await SaveUploadedFilesAsync(UploadedFiles);
            var statusText = BuildOrderStatus(prodoct, ClientName, Contact, Parameters, Staus, uploadedFilePaths);

            var order = new Order
            {
                ClientId = clientId,
                ServiceId = serviceId.Value,
                Staus = statusText,
                CreateAt = DateTime.Now
            };

            _context.Orders.Add(order);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] = "Заявката не беше записана. Провери дали в базата има валиден клиент и валидна услуга.";
                return View(prodoct);
            }

            TempData["SuccessMessage"] = "Заявката е записана успешно.";
            return RedirectToAction(nameof(SubmitRequest), new { id = prodoct.Id });
        }

        // GET: /Prodocts/create
        [HttpGet("create")]
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            LoadCategoriesDropDownList();
            return View();
        }

        // POST: /Prodocts/create
        [HttpPost("create")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Price,Description,CategoryId,CreateAt")] Prodoct prodoct)
        {
            if (!ModelState.IsValid)
            {
                LoadCategoriesDropDownList(prodoct.CategoryId);
                return View(prodoct);
            }

            if (prodoct.CreateAt == default)
            {
                prodoct.CreateAt = DateTime.Now;
            }

            _context.Prodocts.Add(prodoct);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: /Prodocts/edit/5
        [HttpGet("edit/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var prodoct = await _context.Prodocts.FindAsync(id);

            if (prodoct == null)
            {
                return NotFound();
            }

            LoadCategoriesDropDownList(prodoct.CategoryId);
            return View(prodoct);
        }

        // POST: /Prodocts/edit/5
        [HttpPost("edit/{id:int}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Price,Description,CategoryId,CreateAt")] Prodoct prodoct)
        {
            if (id != prodoct.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                LoadCategoriesDropDownList(prodoct.CategoryId);
                return View(prodoct);
            }

            try
            {
                _context.Prodocts.Update(prodoct);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProdoctExists(prodoct.Id))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Prodocts/delete/5
        [HttpGet("delete/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var prodoct = await _context.Prodocts
                .Include(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prodoct == null)
            {
                return NotFound();
            }

            return View(prodoct);
        }

        // POST: /Prodocts/delete/5
        [HttpPost("delete/{id:int}")]
        [ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var prodoct = await _context.Prodocts.FindAsync(id);

            if (prodoct == null)
            {
                return NotFound();
            }

            _context.Prodocts.Remove(prodoct);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Стар линк към клиентска заявка.
        [HttpGet("client")]
        [HttpGet("client/{id:int}")]
        [Authorize]
        public IActionResult Client(int? id)
        {
            if (id.HasValue)
            {
                return RedirectToAction(nameof(SubmitRequest), new { id = id.Value });
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<int?> GetServiceIdForPhotoProductAsync()
        {
            var serviceId = await _context.Services
                .Where(s => s.Name.Contains("Фото"))
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

            if (serviceId != null)
            {
                return serviceId;
            }

            return await _context.Services
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();
        }

        private static string BuildOrderStatus(
            Prodoct prodoct,
            string clientName,
            string contact,
            string parameters,
            string staus,
            List<string> uploadedFilePaths)
        {
            var text = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(staus))
            {
                text.AppendLine(staus.Trim());
            }
            else
            {
                text.AppendLine("Нова заявка за фото продукт");
                text.AppendLine("Тип: Фото продукт");
                text.AppendLine($"Продукт: {prodoct.Name}");
            }

            text.AppendLine();
            text.AppendLine($"Клиент: {clientName.Trim()}");
            text.AppendLine($"Контакт: {contact.Trim()}");
            text.AppendLine($"Цена: {prodoct.Price:0.00} лв.");
            text.AppendLine($"Категория: {(prodoct.Category != null ? prodoct.Category.Name : "Без категория")}");

            if (!string.IsNullOrWhiteSpace(parameters))
            {
                text.AppendLine();
                text.AppendLine("Детайли:");
                text.AppendLine(parameters.Trim());
            }

            if (uploadedFilePaths.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Качени файлове:");
                text.AppendLine(string.Join(Environment.NewLine, uploadedFilePaths));
            }

            return text.ToString().Trim();
        }

        private void LoadCategoriesDropDownList(int? selectedCategoryId = null)
        {
            ViewData["CategoryId"] = new SelectList(
                _context.Categories.AsNoTracking().ToList(),
                "Id",
                "Name",
                selectedCategoryId);
        }

        private async Task AddBuiltInProductsIfMissingAsync()
        {
            if (await _context.Prodocts.AnyAsync())
            {
                return;
            }

            var photoCategory = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name == "Фото продукти");

            if (photoCategory == null)
            {
                photoCategory = new Category
                {
                    Name = "Фото продукти"
                };

                _context.Categories.Add(photoCategory);
                await _context.SaveChangesAsync();
            }

            var createdAt = DateTime.Now;

            var builtInProducts = new List<Prodoct>
            {
                new Prodoct
                {
                    Name = "Фото албум",
                    Price = 29.99m,
                    Description = "Персонализиран фото албум за семейни снимки, празници, балове и специални поводи.",
                    CategoryId = photoCategory.Id,
                    CreateAt = createdAt
                },
                new Prodoct
                {
                    Name = "Фото календар",
                    Price = 14.99m,
                    Description = "Календар със снимки по избор на клиента, подходящ за подарък или офис декорация.",
                    CategoryId = photoCategory.Id,
                    CreateAt = createdAt
                },
                new Prodoct
                {
                    Name = "Фото чаша",
                    Price = 12.00m,
                    Description = "Керамична чаша с отпечатана снимка, надпис или дизайн по желание.",
                    CategoryId = photoCategory.Id,
                    CreateAt = createdAt
                },
                new Prodoct
                {
                    Name = "Фото тениска",
                    Price = 19.99m,
                    Description = "Тениска с персонален печат на снимка, лого или кратък текст.",
                    CategoryId = photoCategory.Id,
                    CreateAt = createdAt
                },
                new Prodoct
                {
                    Name = "Фото рамка",
                    Price = 9.99m,
                    Description = "Декоративна рамка със снимка, подходяща за подарък и украса.",
                    CategoryId = photoCategory.Id,
                    CreateAt = createdAt
                },
                new Prodoct
                {
                    Name = "Плакат със снимка",
                    Price = 16.50m,
                    Description = "Голям цветен плакат със снимка или колаж, подходящ за събития и декорация.",
                    CategoryId = photoCategory.Id,
                    CreateAt = createdAt
                }
            };

            _context.Prodocts.AddRange(builtInProducts);
            await _context.SaveChangesAsync();
        }

        private async Task<List<string>> SaveUploadedFilesAsync(List<IFormFile> uploadedFiles)
        {
            var savedPaths = new List<string>();

            if (uploadedFiles == null || uploadedFiles.Count == 0)
            {
                return savedPaths;
            }

            var webRootPath = _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            var uploadFolder = Path.Combine(webRootPath, "uploads", "photo-products");
            Directory.CreateDirectory(uploadFolder);

            foreach (var file in uploadedFiles)
            {
                if (file == null || file.Length == 0)
                {
                    continue;
                }

                var safeFileName = Path.GetFileName(file.FileName);
                var uniqueFileName = $"{Guid.NewGuid()}_{safeFileName}";
                var fullPath = Path.Combine(uploadFolder, uniqueFileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                savedPaths.Add($"/uploads/photo-products/{uniqueFileName}");
            }

            return savedPaths;
        }

        private bool ProdoctExists(int id)
        {
            return _context.Prodocts.Any(e => e.Id == id);
        }
    }
}
