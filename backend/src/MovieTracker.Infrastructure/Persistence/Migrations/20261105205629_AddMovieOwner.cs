using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "movies",
                type: "uuid",
                nullable: true);

            // Movies without a TMDB ID were shared until now and become private to one user:
            // 1. Movies that nobody has in their diary or watchlist are deleted. They would be invisible to everyone.
            // 2. Movies used by exactly one user are given to that user.
            // 3. Movies used by several users cannot be split automatically, so the migration fails on purpose.
            migrationBuilder.Sql("""
                DELETE FROM movies m
                WHERE m.tmdb_id IS NULL
                  AND NOT EXISTS (SELECT 1 FROM diary_entries d WHERE d.movie_id = m.id)
                  AND NOT EXISTS (SELECT 1 FROM watchlist_items w WHERE w.movie_id = m.id);

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT u.movie_id
                        FROM (SELECT movie_id, user_id FROM diary_entries
                              UNION SELECT movie_id, user_id FROM watchlist_items) u
                        JOIN movies m ON m.id = u.movie_id AND m.tmdb_id IS NULL
                        GROUP BY u.movie_id
                        HAVING COUNT(DISTINCT u.user_id) > 1)
                    THEN
                        RAISE EXCEPTION 'Some movies without a TMDB ID are used by several users. Give them a TMDB ID or remove them from all but one user before migrating.';
                    END IF;
                END $$;

                UPDATE movies m
                SET owner_id = u.user_id
                FROM (SELECT movie_id, user_id FROM diary_entries
                      UNION SELECT movie_id, user_id FROM watchlist_items) u
                WHERE u.movie_id = m.id AND m.tmdb_id IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_movies_owner_id",
                table: "movies",
                column: "owner_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_movies_tmdb_or_owner",
                table: "movies",
                sql: "(tmdb_id IS NULL) <> (owner_id IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_movies_users_owner_id",
                table: "movies",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_movies_users_owner_id",
                table: "movies");

            migrationBuilder.DropIndex(
                name: "ix_movies_owner_id",
                table: "movies");

            migrationBuilder.DropCheckConstraint(
                name: "ck_movies_tmdb_or_owner",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "movies");
        }
    }
}
