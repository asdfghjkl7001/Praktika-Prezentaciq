namespace foto4.Data

{
    public class Service
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Format { get; set; }
        public int DurationInDays { get; set; }
        public int CategoryId { get; set; }
        public Category Categories { get; set; }
        public decimal Price { get; set; }

        public ICollection<Order> Orders { get; set; }
        public DateTime CreateAt { get; set; }
    }
}
