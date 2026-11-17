using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Pessoas;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Dominio.Casas;

/// <summary>
/// A casa monitorada e a raiz do agregado: moradores, rede de cuidado, Sentinela e leituras passam por ela. O endereço é
/// dado pessoal; só o bairro aparece agregado no painel.
/// </summary>
public sealed partial class Domicilio
{
    /// <summary>O máximo de moradores cadastrados numa casa.</summary>
    public const int MaximoDeMoradores = 4;

    private readonly List<Morador> _moradores = [];
    private readonly List<Sentinela> _sentinelas = [];

    private Domicilio(Guid id, string apelido, Endereco endereco, string regiaoClimatica)
    {
        Id = id;
        Apelido = apelido;
        Endereco = endereco;
        RegiaoClimatica = regiaoClimatica;
    }

    /// <summary>Gets o identificador da casa.</summary>
    public Guid Id { get; }

    /// <summary>Gets como a família chama a casa (ex.: "Casa da Dona Lurdes").</summary>
    public string Apelido { get; private set; }

    /// <summary>Gets o endereço.</summary>
    public Endereco Endereco { get; private set; }

    /// <summary>Gets o código da região climática no Clima Service (ex.: <c>SP-MIRANTE</c>).</summary>
    public string RegiaoClimatica { get; }

    /// <summary>Gets os moradores.</summary>
    public IReadOnlyList<Morador> Moradores => _moradores;

    /// <summary>Gets as Sentinelas vinculadas (no protótipo, no máximo uma).</summary>
    public IReadOnlyList<Sentinela> Sentinelas => _sentinelas;

    /// <summary>Gets a rede de cuidado.</summary>
    public RedeDeCuidado RedeDeCuidado { get; } = new();

    /// <summary>Gets a rotina combinada com a pessoa: check-in diário e lembrete de água (R11, R12).</summary>
    public Rotina Rotina { get; private set; } = Rotina.Padrao;

    /// <summary>Gets a value indicating whether a casa está sendo monitorada (regra R8).</summary>
    public bool Monitorado { get; private set; }

    /// <summary>Cadastra uma casa.</summary>
    /// <param name="apelido">Como a família chama a casa.</param>
    /// <param name="endereco">O endereço.</param>
    /// <param name="regiaoClimatica">O código da região climática.</param>
    /// <returns>A casa, ainda sem monitoramento.</returns>
    /// <exception cref="RegraVioladaException">Quando falta o apelido ou a região não segue o formato.</exception>
    public static Domicilio Cadastrar(string apelido, Endereco endereco, string regiaoClimatica)
    {
        ArgumentNullException.ThrowIfNull(endereco);
        if (string.IsNullOrWhiteSpace(apelido) || apelido.Trim().Length > 60)
        {
            throw new RegraVioladaException("CADASTRO", "O apelido da casa é obrigatório e tem até 60 caracteres.");
        }

        string regiao = (regiaoClimatica ?? string.Empty).Trim().ToUpperInvariant();
        if (!FormatoDaRegiao().IsMatch(regiao))
        {
            throw new RegraVioladaException("CADASTRO", "Região climática inválida (ex.: SP-MIRANTE).");
        }

        return new Domicilio(Guid.CreateVersion7(), apelido.Trim(), endereco, regiao);
    }

    /// <summary>Adiciona um morador.</summary>
    /// <param name="morador">O morador.</param>
    /// <exception cref="RegraVioladaException">Acima do máximo de moradores ou com CPF repetido na casa.</exception>
    public void AdicionarMorador(Morador morador)
    {
        ArgumentNullException.ThrowIfNull(morador);
        if (_moradores.Count >= MaximoDeMoradores)
        {
            throw new RegraVioladaException("CADASTRO", "A casa tem no máximo quatro moradores.");
        }

        if (_moradores.Any(m => m.Cpf == morador.Cpf))
        {
            throw new RegraVioladaException("R1", "Esse CPF já está cadastrado nesta casa.");
        }

        _moradores.Add(morador);
    }

    /// <summary>Remove um morador.</summary>
    /// <param name="moradorId">O identificador do morador.</param>
    /// <returns><c>true</c> quando o morador existia.</returns>
    /// <exception cref="RegraVioladaException">Ao remover o último morador de uma casa monitorada (regra R8).</exception>
    public bool RemoverMorador(Guid moradorId)
    {
        if (Monitorado && _moradores.Count == 1 && _moradores[0].Id == moradorId)
        {
            throw new RegraVioladaException("R8", "Desative o monitoramento antes de remover o último morador.");
        }

        return _moradores.RemoveAll(m => m.Id == moradorId) > 0;
    }

    /// <summary>Substitui a rede de cuidado.</summary>
    /// <param name="contatos">Os contatos, na ordem de escalonamento.</param>
    /// <exception cref="RegraVioladaException">Ao esvaziar a rede de uma casa monitorada (regra R8).</exception>
    public void DefinirRedeDeCuidado(IEnumerable<ContatoDeCuidado> contatos)
    {
        ArgumentNullException.ThrowIfNull(contatos);
        ContatoDeCuidado[] novos = [.. contatos];
        if (Monitorado && novos.Length == 0)
        {
            throw new RegraVioladaException("R8", "Uma casa monitorada precisa de pelo menos um contato de cuidado.");
        }

        RedeDeCuidado.Definir(novos);
    }

    /// <summary>Substitui a rotina combinada com a pessoa.</summary>
    /// <param name="rotina">A nova rotina (as regras dela já foram conferidas na criação).</param>
    public void DefinirRotina(Rotina rotina) => Rotina = rotina ?? throw new ArgumentNullException(nameof(rotina));

    /// <summary>Vincula a Sentinela da casa.</summary>
    /// <param name="deviceId">O identificador da Sentinela.</param>
    /// <returns>A Sentinela vinculada.</returns>
    /// <exception cref="RegraVioladaException">Quando a casa já tem uma Sentinela.</exception>
    public Sentinela VincularSentinela(string deviceId)
    {
        Sentinela sentinela = Sentinela.Criar(deviceId);
        if (_sentinelas.Count > 0)
        {
            throw new RegraVioladaException("CADASTRO", "Esta casa já tem uma Sentinela.");
        }

        _sentinelas.Add(sentinela);
        return sentinela;
    }

    /// <summary>Liga o monitoramento (regra R8: precisa de pelo menos um morador e um contato de cuidado).</summary>
    /// <exception cref="RegraVioladaException">Sem morador ou sem contato.</exception>
    public void AtivarMonitoramento()
    {
        if (_moradores.Count == 0 || RedeDeCuidado.Contatos.Count == 0)
        {
            throw new RegraVioladaException("R8", "Para monitorar, cadastre pelo menos um morador e um contato de cuidado.");
        }

        Monitorado = true;
    }

    /// <summary>Desliga o monitoramento.</summary>
    public void DesativarMonitoramento() => Monitorado = false;

    /// <summary>
    /// Os limites térmicos da casa (regra R3): os do morador mais vulnerável, ou os padrão quando não há moradores.
    /// </summary>
    /// <param name="hoje">A data de referência, para a idade.</param>
    /// <returns>Os limites mais baixos entre os moradores.</returns>
    public LimitesTermicos LimitesTermicos(DateOnly hoje)
        => _moradores.Select(m => m.LimitesTermicos(hoje)).MinBy(l => l.Perigo) ?? Calor.LimitesTermicos.Padrao;

    /// <summary>Registra uma medição da Sentinela da casa (regras R9 e R10).</summary>
    /// <param name="deviceId">A Sentinela que mediu.</param>
    /// <param name="momento">O instante da medição.</param>
    /// <param name="temperatura">A temperatura (a faixa física já foi conferida pelo valor).</param>
    /// <param name="umidade">A umidade relativa.</param>
    /// <returns>A leitura classificada pelos limites da casa.</returns>
    /// <exception cref="RegraVioladaException">Quando a Sentinela não pertence a esta casa (R10).</exception>
    public Leitura RegistrarLeitura(string deviceId, DateTimeOffset momento, Temperatura temperatura, UmidadeRelativa umidade)
    {
        ArgumentNullException.ThrowIfNull(temperatura);
        ArgumentNullException.ThrowIfNull(umidade);
        Sentinela sentinela = _sentinelas.SingleOrDefault(s => s.DeviceId == deviceId)
            ?? throw new RegraVioladaException("R10", "Leitura de uma Sentinela que não pertence a esta casa.");

        sentinela.RegistrarContato(momento);
        return new Leitura(sentinela.DeviceId, momento, temperatura, umidade, LimitesTermicos(DateOnly.FromDateTime(momento.UtcDateTime)));
    }

    /// <summary>Nunca revela o endereço.</summary>
    /// <returns>O identificador, sem dado pessoal.</returns>
    public override string ToString() => $"Domicilio {Id}";

    [GeneratedRegex("^[A-Z]{2}-[A-Z0-9-]{2,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoDaRegiao();
}
