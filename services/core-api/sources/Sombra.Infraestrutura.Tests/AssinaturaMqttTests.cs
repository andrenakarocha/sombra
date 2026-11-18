using System;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Infraestrutura.Mqtt;

namespace Sombra.Infraestrutura.Tests;

/// <summary>A assinatura MQTT do Core bate com os vetores compartilhados do contrato (os mesmos da Sentinela e do simulador).</summary>
[TestClass]
public sealed class AssinaturaMqttTests
{
    /// <summary>Cada vetor: a mesma string canônica e a mesma assinatura; uma vírgula a mais derruba a conferência.</summary>
    [TestMethod]
    public void BateComOsVetoresDoContrato()
    {
        using JsonDocument documento = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contratos", "mqtt-assinatura.vetores.json")));
        string segredo = documento.RootElement.GetProperty("segredoHex").GetString()!;
        string deviceId = documento.RootElement.GetProperty("deviceId").GetString()!;
        int conferidos = 0;
        foreach (JsonElement vetor in documento.RootElement.GetProperty("vetores").EnumerateArray())
        {
            JsonElement m = vetor.GetProperty("mensagem");
            long v = m.GetProperty("v").GetInt64();
            long ts = m.GetProperty("ts").GetInt64();
            long seq = m.GetProperty("seq").GetInt64();
            string canonica = vetor.GetProperty("tipo").GetString() switch
            {
                "telemetria" => AssinaturaMqtt.CanonicaDaTelemetria(deviceId, v, ts, seq, m.GetProperty("t").GetDouble(), m.GetProperty("rh").GetDouble(), m.GetProperty("hi").GetDouble(), m.GetProperty("nivel").GetString()!),
                "evento" => AssinaturaMqtt.CanonicaDoEvento(deviceId, v, ts, seq, m.GetProperty("tipo").GetString()!),
                _ => AssinaturaMqtt.CanonicaDoComando(deviceId, v, ts, seq, m.GetProperty("tipo").GetString()!, m.GetProperty("mensagem").GetString()!),
            };
            string sig = vetor.GetProperty("sig").GetString()!;

            Assert.AreEqual(vetor.GetProperty("canonica").GetString(), canonica);
            Assert.AreEqual(sig, AssinaturaMqtt.Assinar(segredo, canonica));
            Assert.IsTrue(AssinaturaMqtt.Conferir(segredo, canonica, sig.ToUpperInvariant()));
            Assert.IsFalse(AssinaturaMqtt.Conferir(segredo, canonica + "|", sig));
            conferidos++;
        }

        Assert.IsGreaterThanOrEqualTo(3, conferidos);
    }
}
