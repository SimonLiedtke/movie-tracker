using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing diary entries and watchlist items have no owner. They get the empty GUID, so adding the
            // foreign keys below fails if there are any: on purpose, rather than deleting data silently.
            // (When this migration was written, no deployed database had any.)
            migrationBuilder.DropPrimaryKey(
                name: "pk_watchlist_items",
                table: "watchlist_items");

            migrationBuilder.DropIndex(
                name: "ix_watchlist_items_added_at",
                table: "watchlist_items");

            migrationBuilder.DropIndex(
                name: "ix_diary_entries_watched_on",
                table: "diary_entries");

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "watchlist_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "diary_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "pk_watchlist_items",
                table: "watchlist_items",
                columns: new[] { "user_id", "movie_id" });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    google_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    picture_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_watchlist_items_movie_id",
                table: "watchlist_items",
                column: "movie_id");

            migrationBuilder.CreateIndex(
                name: "ix_watchlist_items_user_id_added_at",
                table: "watchlist_items",
                columns: new[] { "user_id", "added_at" });

            migrationBuilder.CreateIndex(
                name: "ix_diary_entries_user_id_watched_on",
                table: "diary_entries",
                columns: new[] { "user_id", "watched_on" });

            migrationBuilder.CreateIndex(
                name: "ix_users_google_subject",
                table: "users",
                column: "google_subject",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_diary_entries_users_user_id",
                table: "diary_entries",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_watchlist_items_users_user_id",
                table: "watchlist_items",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_diary_entries_users_user_id",
                table: "diary_entries");

            migrationBuilder.DropForeignKey(
                name: "fk_watchlist_items_users_user_id",
                table: "watchlist_items");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropPrimaryKey(
                name: "pk_watchlist_items",
                table: "watchlist_items");

            migrationBuilder.DropIndex(
                name: "ix_watchlist_items_movie_id",
                table: "watchlist_items");

            migrationBuilder.DropIndex(
                name: "ix_watchlist_items_user_id_added_at",
                table: "watchlist_items");

            migrationBuilder.DropIndex(
                name: "ix_diary_entries_user_id_watched_on",
                table: "diary_entries");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "watchlist_items");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "diary_entries");

            migrationBuilder.AddPrimaryKey(
                name: "pk_watchlist_items",
                table: "watchlist_items",
                column: "movie_id");

            migrationBuilder.CreateIndex(
                name: "ix_watchlist_items_added_at",
                table: "watchlist_items",
                column: "added_at");

            migrationBuilder.CreateIndex(
                name: "ix_diary_entries_watched_on",
                table: "diary_entries",
                column: "watched_on");
        }
    }
}
