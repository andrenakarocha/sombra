using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sombra.Infraestrutura.Persistencia.Migrations
{
    /// <summary>
    /// Os dados da demonstração: três casas fictícias na zona norte de São Paulo (região SP-MIRANTE). Nomes, documentos e
    /// telefones são sintéticos (CPF e CNS com dígito verificador válido, gerados por semente); a Dona Lurdes é a
    /// persona do roteiro (<c>docs/glossario.md</c>). A Dona Benedita ainda não tem rede nem Sentinela, para mostrar o
    /// cadastro em andamento.
    /// </summary>
    public partial class SeedDemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO domicilios (id, apelido, regiao_climatica, monitorado, endereco_logradouro, endereco_numero,
                    endereco_bairro, endereco_cidade, endereco_uf, endereco_cep, rotina_horario_do_check_in_diario,
                    rotina_lembrete_de_agua_ativo, rede_de_cuidado)
                VALUES
                    ('0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10', 'Casa da Dona Lurdes', 'SP-MIRANTE', true, 'Rua das Flores', '12',
                     'Santana', 'São Paulo', 'SP', '02012000', '09:00', true,
                     '[{"nome":"Ana (filha)","telefone":"11987654321","papel":"Familiar"},{"nome":"Rosa (vizinha)","telefone":"11912345678","papel":"Vizinho"}]'),
                    ('4e2b7c90-58a1-4d3f-9b6e-1a7c2d9e8f31', 'Casa do Seu Joaquim', 'SP-MIRANTE', true, 'Rua dos Ipês', '340',
                     'Vila Maria', 'São Paulo', 'SP', '02111000', NULL, true,
                     '[{"nome":"Marcos (filho)","telefone":"11976543210","papel":"Familiar"}]'),
                    ('9c4d1e72-3b5a-4f8e-a2d6-7e1b9c0f4a52', 'Casa da Dona Benedita', 'SP-MIRANTE', false, 'Rua das Acácias', '77',
                     'Tucuruvi', 'São Paulo', 'SP', '02306000', NULL, true, '[]');

                INSERT INTO moradores (id, domicilio_id, nome, data_de_nascimento, cpf, cartao_sus, perfil_de_saude)
                VALUES
                    ('c1a8e2f4-7b3d-4e9a-8f2c-6d5b4a3e2f10', '0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10', 'Lurdes A.', '1948-04-02',
                     '34875763689', '703093750001765', 'USO_DE_DIURETICO'),
                    ('5a9e3d21-7c4b-4e6f-8a1d-2b3c4d5e6f70', '4e2b7c90-58a1-4d3f-9b6e-1a7c2d9e8f31', 'Joaquim B.', '1941-09-15',
                     '03093750024', NULL, 'CARDIOPATIA'),
                    ('e7b1c2d3-4f5a-4b6c-9d8e-0f1a2b3c4d5e', '9c4d1e72-3b5a-4f8e-a2d6-7e1b9c0f4a52', 'Benedita C.', '1944-12-01',
                     '09878483134', '709878483139872', '');

                INSERT INTO sentinelas (device_id, domicilio_id, ultimo_contato)
                VALUES
                    ('snt-7f3a9c', '0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10', NULL),
                    ('snt-2b81d4', '4e2b7c90-58a1-4d3f-9b6e-1a7c2d9e8f31', NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Moradores, Sentinelas, leituras, alertas e check-ins dessas casas saem em cascata.
            migrationBuilder.Sql("""
                DELETE FROM domicilios WHERE id IN ('0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10',
                    '4e2b7c90-58a1-4d3f-9b6e-1a7c2d9e8f31', '9c4d1e72-3b5a-4f8e-a2d6-7e1b9c0f4a52');
                """);
        }
    }
}
