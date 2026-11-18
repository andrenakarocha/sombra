using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sombra.Infraestrutura.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class LinhaDoTempoETelefoneDoMorador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "telefone",
                table: "moradores",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "alertas_linha_do_tempo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    momento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    degrau = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    alerta_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alertas_linha_do_tempo", x => x.id);
                    table.ForeignKey(
                        name: "fk_alertas_linha_do_tempo_alertas_alerta_id",
                        column: x => x.alerta_id,
                        principalTable: "alertas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alertas_linha_do_tempo_alerta_id",
                table: "alertas_linha_do_tempo",
                column: "alerta_id");

            // Os alertas de antes desta migração ganham o primeiro passo, para nenhum aparecer com a linha do tempo vazia.
            migrationBuilder.Sql("""
                INSERT INTO alertas_linha_do_tempo (momento, degrau, descricao, alerta_id)
                SELECT aberto_em, 0,
                       CASE tipo
                           WHEN 'PREVENTIVO' THEN 'Previsão de calor perigoso nas próximas 24 horas.'
                           WHEN 'PEDIDO_DE_AJUDA' THEN 'Pedido de ajuda pela Sentinela ou pelo app.'
                           ELSE 'A Sentinela lembrou e pediu o "estou bem".'
                       END,
                       id
                FROM alertas;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alertas_linha_do_tempo");

            migrationBuilder.DropColumn(
                name: "telefone",
                table: "moradores");
        }
    }
}
