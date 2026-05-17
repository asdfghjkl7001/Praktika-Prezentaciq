namespace foto4.Data

{
    public class Order
    {
        public int Id { get; set; }
        public string ClientId { get; set; }
        public Client Clients { get; set; }
        public int ServiceId { get; set; }
        public Service Services { get; set; }
        public string Staus { get; set; }       
        public DateTime CreateAt { get; set; }
    }
}
