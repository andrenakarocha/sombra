using System;

namespace Sombra.Dominio.Alertas;

/// <summary>
/// Um passo da história do alerta, como o app mostra na linha do tempo: quando, em que degrau e o que aconteceu, em
/// pt-BR e sem dado pessoal.
/// </summary>
/// <param name="Momento">Quando aconteceu, em UTC.</param>
/// <param name="Degrau">O degrau do escalonamento naquele momento.</param>
/// <param name="Descricao">O que aconteceu, numa frase.</param>
public sealed record EventoDaLinhaDoTempo(DateTimeOffset Momento, DegrauDeEscalonamento Degrau, string Descricao);
