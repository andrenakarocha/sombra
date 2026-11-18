using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sombra.Infraestrutura.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class MqttOutboxESeguranca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "segredo_hex",
                table: "sentinelas",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ultimo_seq_enviado",
                table: "sentinelas",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ultimo_seq_recebido",
                table: "sentinelas",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "eventos_de_seguranca",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    momento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    origem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    detalhe = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventos_de_seguranca", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    domicilio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alerta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    destinatario = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mensagem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contatos = table.Column<int>(type: "integer", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    publicada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    ultima_falha = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_eventos_de_seguranca_momento",
                table: "eventos_de_seguranca",
                column: "momento");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_domicilio_id_motivo_criada_em",
                table: "outbox",
                columns: new[] { "domicilio_id", "motivo", "criada_em" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_publicada_em_criada_em",
                table: "outbox",
                columns: new[] { "publicada_em", "criada_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_de_seguranca");

            migrationBuilder.DropTable(
                name: "outbox");

            migrationBuilder.DropColumn(
                name: "segredo_hex",
                table: "sentinelas");

            migrationBuilder.DropColumn(
                name: "ultimo_seq_enviado",
                table: "sentinelas");

            migrationBuilder.DropColumn(
                name: "ultimo_seq_recebido",
                table: "sentinelas");
        }
    }
}
