using Newtonsoft.Json.Linq;



namespace Models
{

    public class Movie
    {
        public bool? Adult { get; set; }
        public string? BackdropPath { get; set; }
        public List<int>? GenreIds { get; set; }
        public int? Id { get; set; }
        public string? OriginalLanguage { get; set; }
        public string? OriginalTitle { get; set; }
        public string? Overview { get; set; }
        public double? Popularity { get; set; }
        public string? PosterPath { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string? Title { get; set; }
        public double? VoteAverage { get; set; }
        public int? VoteCount { get; set; }

        public Movie() { }
        public Movie(string? adult, string? backdropPath, string? genreIds, string? id, string? originalLanguage, string? originalTitle,
                     string? overview, string? popularity, string? posterPath, string? releaseDate, string? title, string? voteAverage, string? voteCount)
        {
            // Set properties using null-coalescing operator (??) to handle potential null values
            Adult = adult?.Contains("true");
            BackdropPath = backdropPath;
            if (genreIds != null)
            {
                JArray genreIdsArray = JArray.Parse(genreIds);
                GenreIds = genreIdsArray.Select(token => (int)token).ToList();
            }
            Id = int.TryParse(id, out int idValue) ? idValue : (int?)null;
            OriginalLanguage = originalLanguage;
            OriginalTitle = originalTitle;
            Overview = overview;
            Popularity = double.TryParse(popularity, out double popularityValue) ? popularityValue : (double?)null;
            PosterPath = posterPath;
            ReleaseDate = DateTime.TryParse(releaseDate, out DateTime releaseDateValue) ? releaseDateValue : (DateTime?)null;
            Title = title;
            VoteAverage = double.TryParse(voteAverage, out double voteAverageValue) ? voteAverageValue : (double?)null;
            VoteCount = int.TryParse(voteCount, out int voteCountValue) ? voteCountValue : (int?)null;
        }

        public override string ToString()
        {
            return $"Id: {Id}, Popularity: {Popularity}, ReleaseDate: {ReleaseDate}, Title: {Title}, VoteAverage: {VoteAverage}, VoteCount: {VoteCount}";
        }

    }
}