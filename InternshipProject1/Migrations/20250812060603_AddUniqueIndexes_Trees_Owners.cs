using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternshipProject1.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexes_Trees_Owners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure phone & nationalId unique (if not already created by model changes)
            migrationBuilder.CreateIndex(
                name: "ux_owners_phone",
                table: "Owners",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_owners_nationalid",
                table: "Owners",
                column: "NationalId",
                unique: true);

            // Case-insensitive unique index for TreeSpecies.Name
            migrationBuilder.Sql(@"
    DO $$
    BEGIN
        IF NOT EXISTS (
            SELECT 1 FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname = 'ux_trees_name_lower'
        ) THEN
            CREATE UNIQUE INDEX ux_trees_name_lower
                ON ""TreeSpecies"" (LOWER(BTRIM(""Name"" )));
        END IF;
    END $$;
");


            // Case-insensitive unique index for Owners.Email
            migrationBuilder.Sql(@"
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public'
                  AND indexname = 'ux_owners_email_lower'
            ) THEN
                CREATE UNIQUE INDEX ux_owners_email_lower
                    ON ""Owners"" (LOWER(""Email""));
            END IF;
        END $$;
    ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop functional indexes
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ux_trees_name_lower;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ux_owners_email_lower;");

            // Drop model indexes (EF will recreate on next migration if needed)
            migrationBuilder.DropIndex(
                name: "ux_owners_phone",
                table: "Owners");

            migrationBuilder.DropIndex(
                name: "ux_owners_nationalid",
                table: "Owners");
        }

    }
}
