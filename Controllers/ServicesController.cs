using foto4.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;

namespace foto4.Controllers
{
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Services
        public async Task<IActionResult> Index()
        {
            // Опитва да запише вградените услуги в базата, ако има поне една категория.
            await SeedDefaultServicesAsync();

            var servicesFromDb = await _context.Services
                .Include(s => s.Categories)
                .ToListAsync();

            // Ако базата е празна или няма категории, пак показваме вградените услуги на страницата.
            var categoryId = await GetFirstCategoryIdAsync();
            var defaultServicesForView = GetDefaultServices(categoryId, includeNavigationCategory: true);

            var existingNames = servicesFromDb
                .Select(s => s.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingDefaultServices = defaultServicesForView
                .Where(s => !existingNames.Contains(s.Name))
                .ToList();

            servicesFromDb.AddRange(missingDefaultServices);

            return View(servicesFromDb);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Client()
        {
            return View();
        }
        // Старият адрес /Services/Order/5 остава да работи, но пренасочва към подаване на заявка.
        [Authorize]
        public IActionResult Order(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(SubmitRequest), new { id = id.Value });
        }

        // GET: Services/SubmitRequest/5
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> SubmitRequest(int? id)
        {
            var service = await GetServiceForViewAsync(id);

            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        // POST: Services/SubmitRequest/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRequest(
            int id,
            string ClientName,
            string Contact,
            string Parameters,
            string Staus,
            List<IFormFile> UploadedFiles)
        {
            var service = await GetServiceForViewAsync(id);

            if (service == null)
            {
                return NotFound();
            }

            var savedFilesCount = 0;

            if (UploadedFiles != null && UploadedFiles.Count > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "requests");
                Directory.CreateDirectory(uploadsFolder);

                foreach (var uploadedFile in UploadedFiles)
                {
                    if (uploadedFile == null || uploadedFile.Length == 0)
                    {
                        continue;
                    }

                    var extension = Path.GetExtension(uploadedFile.FileName);
                    var savedFileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, savedFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await uploadedFile.CopyToAsync(stream);
                    }

                    savedFilesCount++;
                }
            }

            var clientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(clientId))
            {
                return Challenge();
            }

            var newOrder = new Order
            {
                ClientId = clientId,
                ServiceId = id,
                Staus = string.IsNullOrWhiteSpace(Staus) ? "Нова заявка" : Staus,
                CreateAt = DateTime.Now
            };

            _context.Orders.Add(newOrder);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = savedFilesCount == 0
                ? "Заявката беше подадена успешно и беше записана в поръчки."
                : $"Заявката беше подадена успешно, записана е в поръчки и бяха прикачени {savedFilesCount} файл(а).";

            return RedirectToAction(nameof(SubmitRequest), new { id });
        }

        // GET: Services/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var service = await _context.Services
                .Include(s => s.Categories)
                .FirstOrDefaultAsync(m => m.Id == id);

            // Детайли за вградена услуга, която не е записана в базата.
            if (service == null && id < 0)
            {
                var categoryId = await GetFirstCategoryIdAsync();
                service = GetDefaultServices(categoryId, includeNavigationCategory: true)
                    .FirstOrDefault(s => s.Id == id.Value);
            }

            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        // GET: Services/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Id");
            return View();
        }

        // POST: Services/Create
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,Format,DurationInDays,CategoryId,Price,CreateAt")] Service service)
        {
            if (ModelState.IsValid)
            {
                service.CreateAt = service.CreateAt == default ? DateTime.Now : service.CreateAt;

                _context.Add(service);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Id", service.CategoryId);
            return View(service);
        }

        // GET: Services/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Вградените услуги с отрицателно Id не се редактират, защото не са в базата.
            if (id < 0)
            {
                return RedirectToAction(nameof(Index));
            }

            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Id", service.CategoryId);
            return View(service);
        }

        // POST: Services/Edit/5
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Format,DurationInDays,CategoryId,Price,CreateAt")] Service service)
        {
            if (id != service.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(service);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ServiceExists(service.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Id", service.CategoryId);
            return View(service);
        }

        // GET: Services/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Вградените услуги с отрицателно Id не се трият, защото не са записани в базата.
            if (id < 0)
            {
                return RedirectToAction(nameof(Index));
            }

            var service = await _context.Services
                .Include(s => s.Categories)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        // POST: Services/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (id < 0)
            {
                return RedirectToAction(nameof(Index));
            }

            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                _context.Services.Remove(service);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }


        private async Task<Service> GetServiceForViewAsync(int? id)
        {
            if (id == null)
            {
                return null;
            }

            var service = await _context.Services
                .Include(s => s.Categories)
                .FirstOrDefaultAsync(m => m.Id == id);

            // Вградена услуга, която още не е записана в базата.
            if (service == null && id < 0)
            {
                var categoryId = await GetFirstCategoryIdAsync();
                service = GetDefaultServices(categoryId, includeNavigationCategory: true)
                    .FirstOrDefault(s => s.Id == id.Value);
            }

            return service;
        }

        private async Task SeedDefaultServicesAsync()
        {
            var categoryId = await GetFirstCategoryIdAsync();

            // Ако няма категория, не записваме в базата, но Index пак ще покаже вградените услуги.
            if (categoryId == 0)
            {
                return;
            }

            var existingNames = await _context.Services
                .Select(s => s.Name)
                .ToListAsync();

            var existingNamesSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var servicesToAdd = GetDefaultServices(categoryId, includeNavigationCategory: false)
                .Where(s => !existingNamesSet.Contains(s.Name))
                .ToList();

            if (servicesToAdd.Count == 0)
            {
                return;
            }

            // При записване в базата Id трябва да е 0, за да го генерира базата автоматично.
            foreach (var service in servicesToAdd)
            {
                service.Id = 0;
            }

            await _context.Services.AddRangeAsync(servicesToAdd);
            await _context.SaveChangesAsync();
        }

        private async Task<int> GetFirstCategoryIdAsync()
        {
            return await _context.Categories
                .Select(c => c.Id)
                .FirstOrDefaultAsync();
        }

        private List<Service> GetDefaultServices(int categoryId, bool includeNavigationCategory)
        {
            var services = new List<Service>
            {
                new Service
                {
                    Id = -1,
                    Name = "Копиране",
                    Description = "Черно-бяло и цветно копиране на документи с високо качество.",
                    Format = "A4, A3",
                    DurationInDays = 0,
                    CategoryId = categoryId,
                    Price = 0.10m,
                    CreateAt = DateTime.Now
                },
                new Service
                {
                    Id = -2,
                    Name = "Принтиране",
                    Description = "Принтиране на документи, снимки, презентации, проекти и учебни материали.",
                    Format = "A4, A3, PDF, DOCX, JPG",
                    DurationInDays = 0,
                    CategoryId = categoryId,
                    Price = 0.15m,
                    CreateAt = DateTime.Now
                },
                new Service
                {
                    Id = -3,
                    Name = "Сканиране",
                    Description = "Сканиране на документи и изображения в електронен формат.",
                    Format = "PDF, JPG, PNG",
                    DurationInDays = 0,
                    CategoryId = categoryId,
                    Price = 0.50m,
                    CreateAt = DateTime.Now
                },
                new Service
                {
                    Id = -4,
                    Name = "Снимки за документи",
                    Description = "Изработка на снимки за лична карта, паспорт, шофьорска книжка и други документи.",
                    Format = "Според изискванията",
                    DurationInDays = 0,
                    CategoryId = categoryId,
                    Price = 8.00m,
                    CreateAt = DateTime.Now
                },
                
                new Service
                {
                    Id = -6,
                    Name = "Фотосесии",
                    Description = "Портретни, семейни и бизнес фотосесии по предварителна уговорка.",
                    Format = "Дигитални снимки",
                    DurationInDays = 1,
                    CategoryId = categoryId,
                    Price = 50.00m,
                    CreateAt = DateTime.Now
                }
            };

            if (includeNavigationCategory)
            {
                foreach (var service in services)
                {
                    SetNavigationCategory(service, "Вградена категория");
                }
            }

            return services;
        }

        // Попълва navigation property-то Categories, без да знаем точно името на Category класа.
        // Това пази Index view-а от празна категория, ако той показва item.Categories.Id или item.Categories.Name.
        private void SetNavigationCategory(Service service, string categoryName)
        {
            var navigationProperty = typeof(Service).GetProperty("Categories");
            if (navigationProperty == null || !navigationProperty.CanWrite)
            {
                return;
            }

            var categoryObject = Activator.CreateInstance(navigationProperty.PropertyType);
            if (categoryObject == null)
            {
                return;
            }

            SetPropertyValue(categoryObject, "Id", service.CategoryId);
            SetPropertyValue(categoryObject, "Name", categoryName);
            SetPropertyValue(categoryObject, "CategoryName", categoryName);
            SetPropertyValue(categoryObject, "Title", categoryName);

            navigationProperty.SetValue(service, categoryObject);
        }

        private void SetPropertyValue(object obj, string propertyName, object value)
        {
            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite)
            {
                return;
            }

            if (property.PropertyType == typeof(string))
            {
                property.SetValue(obj, value.ToString());
            }
            else if (property.PropertyType == typeof(int))
            {
                property.SetValue(obj, Convert.ToInt32(value));
            }
            else if (property.PropertyType == typeof(int?))
            {
                property.SetValue(obj, Convert.ToInt32(value));
            }
        }

        private bool ServiceExists(int id)
        {
            return _context.Services.Any(e => e.Id == id);
        }
    }
}
