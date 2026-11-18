using Microsoft.EntityFrameworkCore;
using Sombra.Dominio.Casas;
using Sombra.Privacidade.Repouso;

namespace Sombra.Infraestrutura.Persistencia.Configuracoes;

/// <summary>
/// Todos os campos cifrados em repouso num lugar só (spec 07 §1, LGPD art. 46): CPF, Cartão SUS, telefone, perfil e
/// observações de saúde do morador e a rede de cuidado da casa (nomes e telefones). O CPF ganha o índice cego
/// <c>cpf_indice</c>, que garante "sem CPF repetido na casa" sem decifrar nada.
/// </summary>
internal static class CamposSensiveis
{
    /// <summary>A propriedade sombra com o índice cego do CPF.</summary>
    internal const string CpfIndice = "CpfIndice";

    /// <summary>As colunas cifradas, como entram no dado autenticado do AES-GCM (não mudar: os cifrados dependem disso).</summary>
    internal const string ColunaCpf = "moradores.cpf";

    /// <summary>O Cartão SUS.</summary>
    internal const string ColunaCartaoSus = "moradores.cartao_sus";

    /// <summary>O telefone do morador.</summary>
    internal const string ColunaTelefone = "moradores.telefone";

    /// <summary>Os fatores de risco declarados.</summary>
    internal const string ColunaPerfil = "moradores.perfil_de_saude";

    /// <summary>O texto livre de saúde.</summary>
    internal const string ColunaObservacoes = "moradores.observacoes_de_saude";

    /// <summary>A rede de cuidado.</summary>
    internal const string ColunaRede = "domicilios.rede_de_cuidado";

    /// <summary>Aplica as conversões cifradas e o índice cego.</summary>
    /// <param name="modelo">O modelo.</param>
    /// <param name="cifra">A cifra do ambiente.</param>
    internal static void Configurar(ModelBuilder modelo, CifraDeCampos cifra)
    {
        modelo.Entity<Morador>(morador =>
        {
            morador.Property(m => m.Cpf).HasConversion(Conversores.CpfCifrado(cifra, ColunaCpf));
            morador.Property(m => m.CartaoSus).HasConversion(Conversores.CartaoSusCifrado(cifra, ColunaCartaoSus));
            morador.Property(m => m.Telefone).HasConversion(Conversores.TelefoneCifrado(cifra, ColunaTelefone));
            morador.Property(m => m.PerfilDeSaude).HasConversion(Conversores.PerfilCifrado(cifra, ColunaPerfil));
            morador.Property(m => m.ObservacoesDeSaude).HasConversion(Conversores.TextoCifrado(cifra, ColunaObservacoes));
            morador.Property<string>(CpfIndice).HasMaxLength(64);
            morador.HasIndex("DomicilioId", CpfIndice).IsUnique();
        });

        modelo.Entity<Domicilio>().Property(d => d.RedeDeCuidado)
            .HasConversion(Conversores.RedeCifrada(cifra, ColunaRede), Conversores.RedePorConteudo);
    }
}
