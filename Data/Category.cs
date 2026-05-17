namespace foto4.Data

{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ICollection<Prodoct> Prodocts { get; set; }
        public ICollection<Service> Services { get; set; }
    }
}
