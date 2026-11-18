using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sombra.Infraestrutura.Persistencia.Migrations
{
    /// <summary>
    /// F2.6: cada evento de segurança guarda as três traduções (cidadão, gestão, sugestão técnica), a fonte (IA ou
    /// template) e a assinatura do cache. <c>traduzido_em</c> nulo é a fila do tradutor (o outbox).
    /// </summary>
    public partial class TraducaoDeRisco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "assinatura",
                table: "eventos_de_seguranca",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fonte_da_traducao",
                table: "eventos_de_seguranca",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "traducao_cidadao",
                table: "eventos_de_seguranca",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "traducao_gestao",
                table: "eventos_de_seguranca",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "traducao_tecnica",
                table: "eventos_de_seguranca",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "traduzido_em",
                table: "eventos_de_seguranca",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventos_de_seguranca_assinatura_fonte_da_traducao",
                table: "eventos_de_seguranca",
                columns: new[] { "assinatura", "fonte_da_traducao" });

            migrationBuilder.CreateIndex(
                name: "ix_eventos_de_seguranca_traduzido_em",
                table: "eventos_de_seguranca",
                column: "traduzido_em");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_eventos_de_seguranca_assinatura_fonte_da_traducao",
                table: "eventos_de_seguranca");

            migrationBuilder.DropIndex(
                name: "ix_eventos_de_seguranca_traduzido_em",
                table: "eventos_de_seguranca");

            migrationBuilder.DropColumn(
                name: "assinatura",
                table: "eventos_de_seguranca");

            migrationBuilder.DropColumn(
                name: "fonte_da_traducao",
                table: "eventos_de_seguranca");

            migrationBuilder.DropColumn(
                name: "traducao_cidadao",
                table: "eventos_de_seguranca");

            migrationBuilder.DropColumn(
                name: "traducao_gestao",
                table: "eventos_de_seguranca");

            migrationBuilder.DropColumn(
                name: "traducao_tecnica",
                table: "eventos_de_seguranca");

            migrationBuilder.DropColumn(
                name: "traduzido_em",
                table: "eventos_de_seguranca");
        }
    }
}
