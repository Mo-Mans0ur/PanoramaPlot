namespace Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; } // Store hashed passwords

        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    }
}