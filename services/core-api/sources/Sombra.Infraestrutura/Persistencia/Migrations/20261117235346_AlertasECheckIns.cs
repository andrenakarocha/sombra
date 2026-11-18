using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sombra.Infraestrutura.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AlertasECheckIns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alertas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    domicilio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nivel = table.Column<int>(type: "integer", nullable: false),
                    aberto_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    degrau = table.Column<int>(type: "integer", nullable: false),
                    prazo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reconhecimento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    encerrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ultimo_degrau_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alertas", x => x.id);
                    table.ForeignKey(
                        name: "fk_alertas_domicilios_domicilio_id",
                        column: x => x.domicilio_id,
                        principalTable: "domicilios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "check_ins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    domicilio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    momento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_check_ins", x => x.id);
                    table.ForeignKey(
                        name: "fk_check_ins_domicilios_domicilio_id",
                        column: x => x.domicilio_id,
                        principalTable: "domicilios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alertas_domicilio_id",
                table: "alertas",
                column: "domicilio_id");

            migrationBuilder.CreateIndex(
                name: "ix_check_ins_domicilio_id",
                table: "check_ins",
                column: "domicilio_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alertas");

            migrationBuilder.DropTable(
                name: "check_ins");
        }
    }
}
