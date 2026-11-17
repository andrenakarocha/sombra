"""Verificação de comandos recebidos: assinatura, anti-replay e janela de tempo."""

from sentinela_sim import assinatura

DEVICE = "snt-000001"
SEGREDO = "ab" * 32
AGORA = 1_763_400_000


def comando(seq: int = 5, ts: int = AGORA, mensagem: str = "Beba água.") -> dict:
    m = {"v": 1, "ts": ts, "seq": seq, "tipo": "LEMBRETE_AGUA", "mensagem": mensagem}
    return assinatura.assinada("comando", DEVICE, SEGREDO, m)


def verificar(m: dict, ultimo_seq: int = 4, agora: float = AGORA) -> str | None:
    return assinatura.verificar("comando", DEVICE, SEGREDO, m, ultimo_seq, agora)


def test_aceita_comando_valido():
    assert verificar(comando()) is None


def test_recusa_mensagem_alterada_depois_de_assinada():
    m = comando()
    m["mensagem"] = "Abra a porta."
    assert verificar(m) == "assinatura_invalida"


def test_recusa_comando_de_outra_sentinela():
    m = comando()
    assert assinatura.verificar("comando", "snt-999999", SEGREDO, m, 4, AGORA) == "assinatura_invalida"


def test_recusa_replay_do_mesmo_seq():
    assert verificar(comando(seq=4), ultimo_seq=4) == "replay"


def test_recusa_fora_da_janela_de_tempo():
    assert verificar(comando(ts=AGORA - 301)) == "fora_da_janela"
    assert verificar(comando(ts=AGORA - 300)) is None


def test_recusa_campos_faltando():
    assert verificar({"v": 1, "sig": "00"}) == "campos_invalidos"
