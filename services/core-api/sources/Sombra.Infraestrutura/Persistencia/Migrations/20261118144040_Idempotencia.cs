using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sombra.Infraestrutura.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Idempotencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "requisicoes_idempotentes",
                columns: table => new
                {
                    usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rota = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: true),
                    corpo = table.Column<string>(type: "text", nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requisicoes_idempotentes", x => new { x.usuario, x.chave });
                });

            migrationBuilder.CreateIndex(
                name: "ix_requisicoes_idempotentes_criada_em",
                table: "requisicoes_idempotentes",
                column: "criada_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "requisicoes_idempotentes");
        }
    }
}
