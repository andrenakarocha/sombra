using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sombra.Infraestrutura.Persistencia.Migrations
{
    /// <summary>
    /// F2.3: as colunas de PII viram texto para caber o cifrado AES-256-GCM (<c>v1:</c>...), a rede de cuidado deixa de ser
    /// jsonb (o JSON vai cifrado) e o "sem CPF repetido na casa" passa para o índice cego <c>cpf_indice</c>. A cifra em si
    /// não cabe numa migração (a chave é do ambiente): o Core cifra as linhas em claro ao subir
    /// (<c>SombraDbContext.CifrarLegadoAsync</c>). O Down só serve para um banco que ainda não tem cifrados.
    /// </summary>
    public partial class CamposSensiveisCifrados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_moradores_domicilio_id_cpf",
                table: "moradores");

            migrationBuilder.AlterColumn<string>(
                name: "telefone",
                table: "moradores",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(11)",
                oldMaxLength: 11,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "perfil_de_saude",
                table: "moradores",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "observacoes_de_saude",
                table: "moradores",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "cpf",
                table: "moradores",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(11)",
                oldMaxLength: 11);

            migrationBuilder.AlterColumn<string>(
                name: "cartao_sus",
                table: "moradores",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(15)",
                oldMaxLength: 15,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cpf_indice",
                table: "moradores",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "rede_de_cuidado",
                table: "domicilios",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateIndex(
                name: "ix_moradores_domicilio_id_cpf_indice",
                table: "moradores",
                columns: new[] { "domicilio_id", "cpf_indice" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_moradores_domicilio_id_cpf_indice",
                table: "moradores");

            migrationBuilder.DropColumn(
                name: "cpf_indice",
                table: "moradores");

            migrationBuilder.AlterColumn<string>(
                name: "telefone",
                table: "moradores",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "perfil_de_saude",
                table: "moradores",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "observacoes_de_saude",
                table: "moradores",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "cpf",
                table: "moradores",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "cartao_sus",
                table: "moradores",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "rede_de_cuidado",
                table: "domicilios",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "ix_moradores_domicilio_id_cpf",
                table: "moradores",
                columns: new[] { "domicilio_id", "cpf" },
                unique: true);
        }
    }
}
