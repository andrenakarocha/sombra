"""Cenários determinísticos do simulador.

Cada cenário é uma sequência de passos (acao, dado):
- ("telemetria", (temperatura_c, umidade_pct))
- ("adulterada", (temperatura_c, umidade_pct))  telemetria alterada depois de assinada
- ("evento", tipo)                                CHECKIN_ESTOU_BEM, PEDIDO_DE_AJUDA ou BOOT
- ("esperar", n)                                  n intervalos sem enviar nada
"""

from collections.abc import Callable, Iterator

Passo = tuple[str, object]


def _rampa(de: float, ate: float, passo: float) -> Iterator[float]:
    n = round((ate - de) / passo)
    for i in range(n + 1):
        yield round(de + i * passo, 1)


def normal() -> Iterator[Passo]:
    yield ("evento", "BOOT")
    for t in (24.0, 24.2, 24.5, 24.3, 24.1, 24.0):
        yield ("telemetria", (t, 55.0))


def onda_de_calor() -> Iterator[Passo]:
    """A casa esquenta até PERIGO e a pessoa confirma que está bem (caminho A do roteiro)."""
    yield ("evento", "BOOT")
    for t in _rampa(28.0, 36.0, 0.8):
        yield ("telemetria", (t, 60.0))
    yield ("esperar", 3)
    yield ("evento", "CHECKIN_ESTOU_BEM")
    for t in (35.0, 33.5, 32.0):
        yield ("telemetria", (t, 58.0))


def sem_resposta() -> Iterator[Passo]:
    """A casa chega a PERIGO e ninguém aperta o botão (caminho B do roteiro)."""
    yield ("evento", "BOOT")
    for t in _rampa(28.0, 36.0, 0.8):
        yield ("telemetria", (t, 60.0))
    for _ in range(12):
        yield ("telemetria", (36.0, 60.0))


def pedido_de_ajuda() -> Iterator[Passo]:
    yield ("evento", "BOOT")
    for t in (30.0, 31.0, 32.0):
        yield ("telemetria", (t, 55.0))
    yield ("evento", "PEDIDO_DE_AJUDA")


def esqueceu_checkin_diario() -> Iterator[Passo]:
    """Dia ameno sem nenhum check-in: o Core só pode mandar lembrete leve (D13)."""
    yield ("evento", "BOOT")
    for t in (23.0, 23.4, 23.8, 24.0, 24.2, 24.0, 23.6, 23.2):
        yield ("telemetria", (t, 60.0))


def payload_adulterado() -> Iterator[Passo]:
    """Uma telemetria válida, uma alterada depois de assinada (o Core deve recusar) e outra válida."""
    yield ("evento", "BOOT")
    yield ("telemetria", (30.0, 50.0))
    yield ("adulterada", (30.0, 50.0))
    yield ("telemetria", (30.2, 50.0))


CENARIOS: dict[str, tuple[Callable[[], Iterator[Passo]], str]] = {
    "normal": (normal, "dia comum, tudo NORMAL"),
    "onda-de-calor": (onda_de_calor, "a casa chega a PERIGO e ela aperta ESTOU BEM"),
    "sem-resposta": (sem_resposta, "a casa chega a PERIGO e ninguém responde"),
    "pedido-de-ajuda": (pedido_de_ajuda, "o botão vermelho é pressionado"),
    "esqueceu-checkin-diario": (esqueceu_checkin_diario, "dia ameno sem check-in diário"),
    "payload-adulterado": (payload_adulterado, "uma telemetria alterada depois de assinada"),
}
