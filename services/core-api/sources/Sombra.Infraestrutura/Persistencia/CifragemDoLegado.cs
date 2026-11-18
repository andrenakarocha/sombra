using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Sombra.Infraestrutura.Persistencia.Configuracoes;
using Sombra.Privacidade.Repouso;

namespace Sombra.Infraestrutura.Persistencia;

/// <summary>
/// Cifra o que foi gravado em claro antes da F2.3 (o seed da demo e bancos locais): lê cada linha sem o prefixo
/// <c>v1:</c>, cifra no C# com a chave do ambiente e grava, numa transação. Roda depois das migrações; numa segunda vez
/// não acha nada. Os conversores do EF continuam estritos: texto em claro no banco é erro.
/// </summary>
internal static class CifragemDoLegado
{
    /// <summary>Cifra as linhas legadas.</summary>
    /// <param name="conexao">A conexão do contexto.</param>
    /// <param name="cifra">A cifra.</param>
    /// <param name="cancellationToken">Cancela.</param>
    /// <returns>Quantas linhas foram cifradas (moradores e casas).</returns>
    internal static async Task<int> CifrarAsync(NpgsqlConnection conexao, CifraDeCampos cifra, CancellationToken cancellationToken)
    {
        bool abriu = conexao.State != ConnectionState.Open;
        if (abriu)
        {
            await conexao.OpenAsync(cancellationToken);
        }

        try
        {
            await using NpgsqlTransaction transacao = await conexao.BeginTransactionAsync(cancellationToken);
            int total = await MoradoresAsync(conexao, transacao, cifra, cancellationToken) + await CasasAsync(conexao, transacao, cifra, cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return total;
        }
        finally
        {
            if (abriu)
            {
                await conexao.CloseAsync();
            }
        }
    }

    private static async Task<int> MoradoresAsync(NpgsqlConnection conexao, NpgsqlTransaction transacao, CifraDeCampos cifra, CancellationToken cancellationToken)
    {
        List<(Guid Id, string Cpf, string? Cns, string? Telefone, string? Perfil, string? Observacoes)> linhas = [];
        await using (NpgsqlCommand consulta = new("SELECT id, cpf, cartao_sus, telefone, perfil_de_saude, observacoes_de_saude FROM moradores WHERE cpf NOT LIKE 'v1:%' FOR UPDATE", conexao, transacao))
        await using (NpgsqlDataReader leitor = await consulta.ExecuteReaderAsync(cancellationToken))
        {
            while (await leitor.ReadAsync(cancellationToken))
            {
                linhas.Add((leitor.GetGuid(0), leitor.GetString(1), Texto(leitor, 2), Texto(leitor, 3), Texto(leitor, 4), Texto(leitor, 5)));
            }
        }

        foreach ((Guid id, string cpf, string? cns, string? telefone, string? perfil, string? observacoes) in linhas)
        {
            await using NpgsqlCommand atualizar = new(
                "UPDATE moradores SET cpf = @cpf, cpf_indice = @indice, cartao_sus = @cns, telefone = @telefone, perfil_de_saude = @perfil, observacoes_de_saude = @observacoes WHERE id = @id",
                conexao,
                transacao);
            atualizar.Parameters.AddWithValue("id", id);
            atualizar.Parameters.AddWithValue("cpf", cifra.Cifrar(cpf, CamposSensiveis.ColunaCpf));
            atualizar.Parameters.AddWithValue("indice", cifra.IndiceCego(cpf, CamposSensiveis.ColunaCpf));
            atualizar.Parameters.AddWithValue("cns", Cifrar(cifra, cns, CamposSensiveis.ColunaCartaoSus));
            atualizar.Parameters.AddWithValue("telefone", Cifrar(cifra, telefone, CamposSensiveis.ColunaTelefone));
            atualizar.Parameters.AddWithValue("perfil", Cifrar(cifra, perfil, CamposSensiveis.ColunaPerfil));
            atualizar.Parameters.AddWithValue("observacoes", Cifrar(cifra, observacoes, CamposSensiveis.ColunaObservacoes));
            await atualizar.ExecuteNonQueryAsync(cancellationToken);
        }

        return linhas.Count;
    }

    private static async Task<int> CasasAsync(NpgsqlConnection conexao, NpgsqlTransaction transacao, CifraDeCampos cifra, CancellationToken cancellationToken)
    {
        List<(Guid Id, string Rede)> linhas = [];
        await using (NpgsqlCommand consulta = new("SELECT id, rede_de_cuidado FROM domicilios WHERE rede_de_cuidado NOT LIKE 'v1:%' FOR UPDATE", conexao, transacao))
        await using (NpgsqlDataReader leitor = await consulta.ExecuteReaderAsync(cancellationToken))
        {
            while (await leitor.ReadAsync(cancellationToken))
            {
                linhas.Add((leitor.GetGuid(0), leitor.GetString(1)));
            }
        }

        foreach ((Guid id, string rede) in linhas)
        {
            await using NpgsqlCommand atualizar = new("UPDATE domicilios SET rede_de_cuidado = @rede WHERE id = @id", conexao, transacao);
            atualizar.Parameters.AddWithValue("id", id);
            atualizar.Parameters.AddWithValue("rede", cifra.Cifrar(rede, CamposSensiveis.ColunaRede));
            await atualizar.ExecuteNonQueryAsync(cancellationToken);
        }

        return linhas.Count;
    }

    private static string? Texto(NpgsqlDataReader leitor, int coluna) => leitor.IsDBNull(coluna) ? null : leitor.GetString(coluna);

    private static object Cifrar(CifraDeCampos cifra, string? valor, string coluna)
        => valor is null ? DBNull.Value : cifra.Cifrar(valor, coluna);
}
