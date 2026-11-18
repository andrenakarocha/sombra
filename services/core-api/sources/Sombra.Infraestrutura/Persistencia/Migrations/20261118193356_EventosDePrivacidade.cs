using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sombra.Infraestrutura.Persistencia.Migrations
{
    /// <summary>
    /// F2.5: a trilha de segurança passa a guardar também os vazamentos que os guardas do <c>Sombra.Privacidade</c>
    /// barraram, com casa, sink, família, severidade, regra e desfecho, que é de onde o farol do Risco Cidadão sai. Só
    /// acrescenta colunas: os eventos MQTT que já existem continuam valendo como bloqueados.
    /// </summary>
    public partial class EventosDePrivacidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "bloqueado",
                table: "eventos_de_seguranca",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "categoria",
                table: "eventos_de_seguranca",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "domicilio_id",
                table: "eventos_de_seguranca",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "regra",
                table: "eventos_de_seguranca",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "severidade",
                table: "eventos_de_seguranca",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sink",
                table: "eventos_de_seguranca",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventos_de_seguranca_domicilio_id_momento",
                table: "eventos_de_seguranca",
                columns: new[] { "domicilio_id", "momento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_eventos_de_seguranca_domicilio_id_momento",
                table: "eventos_de_seguranca");

            migrationBuilder.DropColumn(
                name: "bloqueado",
                table: "eventos_de_seguranca");

            migrationBuilder.DropColumn(
                name: "categoria",
                table: "eventos_de_seguranca");

            migrationBuilder.DropColumn(
                name: "domicilio_id",
                table: "eventos_de_seguranca");

            migrationBuilder.DropColumn(
                name: "regra",
                table: "eventos_de_seguranca");

            migrationBuilder.DropColumn(
                name: "severidade",
                table: "eventos_de_seguranca");

            migrationBuilder.DropColumn(
                name: "sink",
                table: "eventos_de_seguranca");
        }
    }
}
