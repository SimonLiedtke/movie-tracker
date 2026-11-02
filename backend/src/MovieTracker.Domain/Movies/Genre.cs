namespace MovieTracker.Domain.Movies;

public sealed class Genre
{
    public Genre(int id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }
}
