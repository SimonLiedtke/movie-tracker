using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MovieTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "genres",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_genres", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "movies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    original_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    runtime_minutes = table.Column<int>(type: "integer", nullable: true),
                    overview = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    poster_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    tmdb_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movies", x => x.id);
                    table.CheckConstraint("ck_movies_runtime_minutes_positive", "runtime_minutes > 0");
                });

            migrationBuilder.CreateTable(
                name: "diary_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    movie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    watched_on = table.Column<DateOnly>(type: "date", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: true),
                    review = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    is_rewatch = table.Column<bool>(type: "boolean", nullable: false),
                    liked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_diary_entries", x => x.id);
                    table.CheckConstraint("ck_diary_entries_rating_range", "rating BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "fk_diary_entries_movies_movie_id",
                        column: x => x.movie_id,
                        principalTable: "movies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movie_genres",
                columns: table => new
                {
                    genre_id = table.Column<int>(type: "integer", nullable: false),
                    movie_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movie_genres", x => new { x.genre_id, x.movie_id });
                    table.ForeignKey(
                        name: "fk_movie_genres_genres_genre_id",
                        column: x => x.genre_id,
                        principalTable: "genres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_movie_genres_movies_movie_id",
                        column: x => x.movie_id,
                        principalTable: "movies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "watchlist_items",
                columns: table => new
                {
                    movie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_watchlist_items", x => x.movie_id);
                    table.ForeignKey(
                        name: "fk_watchlist_items_movies_movie_id",
                        column: x => x.movie_id,
                        principalTable: "movies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "genres",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 12, "Adventure" },
                    { 14, "Fantasy" },
                    { 16, "Animation" },
                    { 18, "Drama" },
                    { 27, "Horror" },
                    { 28, "Action" },
                    { 35, "Comedy" },
                    { 36, "History" },
                    { 37, "Western" },
                    { 53, "Thriller" },
                    { 80, "Crime" },
                    { 99, "Documentary" },
                    { 878, "Science Fiction" },
                    { 9648, "Mystery" },
                    { 10402, "Music" },
                    { 10749, "Romance" },
                    { 10751, "Family" },
                    { 10752, "War" },
                    { 10770, "TV Movie" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_diary_entries_movie_id",
                table: "diary_entries",
                column: "movie_id");

            migrationBuilder.CreateIndex(
                name: "ix_diary_entries_watched_on",
                table: "diary_entries",
                column: "watched_on");

            migrationBuilder.CreateIndex(
                name: "ix_genres_name",
                table: "genres",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movie_genres_movie_id",
                table: "movie_genres",
                column: "movie_id");

            migrationBuilder.CreateIndex(
                name: "ix_movies_tmdb_id",
                table: "movies",
                column: "tmdb_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_watchlist_items_added_at",
                table: "watchlist_items",
                column: "added_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "diary_entries");

            migrationBuilder.DropTable(
                name: "movie_genres");

            migrationBuilder.DropTable(
                name: "watchlist_items");

            migrationBuilder.DropTable(
                name: "genres");

            migrationBuilder.DropTable(
                name: "movies");
        }
    }
}
