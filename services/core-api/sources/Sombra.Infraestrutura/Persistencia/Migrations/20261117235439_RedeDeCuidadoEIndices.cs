using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sombra.Infraestrutura.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RedeDeCuidadoEIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_moradores_domicilio_id",
                table: "moradores");

            migrationBuilder.DropIndex(
                name: "ix_leituras_domicilio_id",
                table: "leituras");

            migrationBuilder.DropIndex(
                name: "ix_check_ins_domicilio_id",
                table: "check_ins");

            migrationBuilder.DropIndex(
                name: "ix_alertas_domicilio_id",
                table: "alertas");

            migrationBuilder.AddColumn<string>(
                name: "rede_de_cuidado",
                table: "domicilios",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateIndex(
                name: "ix_moradores_domicilio_id_cpf",
                table: "moradores",
                columns: new[] { "domicilio_id", "cpf" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_leituras_domicilio_id_momento",
                table: "leituras",
                columns: new[] { "domicilio_id", "momento" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_check_ins_domicilio_id_momento",
                table: "check_ins",
                columns: new[] { "domicilio_id", "momento" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_alertas_domicilio_id_estado",
                table: "alertas",
                columns: new[] { "domicilio_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_alertas_estado_aberto_em",
                table: "alertas",
                columns: new[] { "estado", "aberto_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_moradores_domicilio_id_cpf",
                table: "moradores");

            migrationBuilder.DropIndex(
                name: "ix_leituras_domicilio_id_momento",
                table: "leituras");

            migrationBuilder.DropIndex(
                name: "ix_check_ins_domicilio_id_momento",
                table: "check_ins");

            migrationBuilder.DropIndex(
                name: "ix_alertas_domicilio_id_estado",
                table: "alertas");

            migrationBuilder.DropIndex(
                name: "ix_alertas_estado_aberto_em",
                table: "alertas");

            migrationBuilder.DropColumn(
                name: "rede_de_cuidado",
                table: "domicilios");

            migrationBuilder.CreateIndex(
                name: "ix_moradores_domicilio_id",
                table: "moradores",
                column: "domicilio_id");

            migrationBuilder.CreateIndex(
                name: "ix_leituras_domicilio_id",
                table: "leituras",
                column: "domicilio_id");

            migrationBuilder.CreateIndex(
                name: "ix_check_ins_domicilio_id",
                table: "check_ins",
                column: "domicilio_id");

            migrationBuilder.CreateIndex(
                name: "ix_alertas_domicilio_id",
                table: "alertas",
                column: "domicilio_id");
        }
    }
}
