using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sombra.Infraestrutura.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "domicilios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    apelido = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    regiao_climatica = table.Column<string>(type: "character varying(33)", maxLength: 33, nullable: false),
                    monitorado = table.Column<bool>(type: "boolean", nullable: false),
                    endereco_bairro = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    endereco_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    endereco_cidade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    endereco_logradouro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    endereco_numero = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    endereco_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    rotina_horario_do_check_in_diario = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    rotina_lembrete_de_agua_ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_domicilios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leituras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    momento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    temperatura_c = table.Column<double>(type: "double precision", nullable: false),
                    umidade_pct = table.Column<double>(type: "double precision", nullable: false),
                    nivel = table.Column<int>(type: "integer", nullable: false),
                    domicilio_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leituras", x => x.id);
                    table.ForeignKey(
                        name: "fk_leituras_domicilios_domicilio_id",
                        column: x => x.domicilio_id,
                        principalTable: "domicilios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "moradores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    data_de_nascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    cartao_sus = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    perfil_de_saude = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    domicilio_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moradores", x => x.id);
                    table.ForeignKey(
                        name: "fk_moradores_domicilios_domicilio_id",
                        column: x => x.domicilio_id,
                        principalTable: "domicilios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sentinelas",
                columns: table => new
                {
                    device_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ultimo_contato = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    domicilio_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sentinelas", x => x.device_id);
                    table.ForeignKey(
                        name: "fk_sentinelas_domicilios_domicilio_id",
                        column: x => x.domicilio_id,
                        principalTable: "domicilios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_leituras_domicilio_id",
                table: "leituras",
                column: "domicilio_id");

            migrationBuilder.CreateIndex(
                name: "ix_moradores_domicilio_id",
                table: "moradores",
                column: "domicilio_id");

            migrationBuilder.CreateIndex(
                name: "ix_sentinelas_domicilio_id",
                table: "sentinelas",
                column: "domicilio_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leituras");

            migrationBuilder.DropTable(
                name: "moradores");

            migrationBuilder.DropTable(
                name: "sentinelas");

            migrationBuilder.DropTable(
                name: "domicilios");
        }
    }
}
