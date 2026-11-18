using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sombra.Infraestrutura.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class EscritasDoApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "observacoes_de_saude",
                table: "moradores",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "rotina_check_in_diario_ativo",
                table: "domicilios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "rotina_horario_escolhido",
                table: "domicilios",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(9, 0, 0));

            // O horário combinado de cada casa passa para as colunas novas antes de a antiga sair (D17: desligado guarda o horário).
            migrationBuilder.Sql("""
                UPDATE domicilios
                SET rotina_check_in_diario_ativo = rotina_horario_do_check_in_diario IS NOT NULL,
                    rotina_horario_escolhido = COALESCE(rotina_horario_do_check_in_diario, TIME '09:00');
                """);

            migrationBuilder.DropColumn(
                name: "rotina_horario_do_check_in_diario",
                table: "domicilios");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "rotina_horario_do_check_in_diario",
                table: "domicilios",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE domicilios
                SET rotina_horario_do_check_in_diario = CASE WHEN rotina_check_in_diario_ativo THEN rotina_horario_escolhido END;
                """);

            migrationBuilder.DropColumn(
                name: "observacoes_de_saude",
                table: "moradores");

            migrationBuilder.DropColumn(
                name: "rotina_check_in_diario_ativo",
                table: "domicilios");

            migrationBuilder.DropColumn(
                name: "rotina_horario_escolhido",
                table: "domicilios");
        }
    }
}
