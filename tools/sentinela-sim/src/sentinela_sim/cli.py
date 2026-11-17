"""Linha de comando do simulador. As opções caem nas variáveis do .env (make env)."""

import argparse
import json
import os
import sys
import time

from . import log
from .cenarios import CENARIOS
from .sentinela import ESTADO_PADRAO, Sentinela, publicar_comando


def _comum(p: argparse.ArgumentParser) -> None:
    p.add_argument("--device", default=os.environ.get("SENTINELA_DEVICE_ID", "snt-000001"))
    p.add_argument(
        "--segredo",
        default=os.environ.get("SENTINELA_SEGREDO_HEX", ""),
        help="32 bytes em hexadecimal (padrão: SENTINELA_SEGREDO_HEX)",
    )
    p.add_argument("--host", default=os.environ.get("MQTT_HOST", "localhost"))
    p.add_argument("--porta", type=int, default=int(os.environ.get("MQTT_PORTA", "1883")))


def _rodar(args: argparse.Namespace) -> int:
    sentinela = Sentinela(args.device, args.segredo, args.host, args.porta)
    sentinela.conectar()
    try:
        for acao, dado in CENARIOS[args.cenario][0]():
            if acao == "telemetria":
                sentinela.telemetria(*dado)
            elif acao == "adulterada":
                sentinela.telemetria(*dado, adulterar=True)
            elif acao == "evento":
                sentinela.evento(dado)
            elif acao == "esperar":
                time.sleep(args.intervalo * dado)
                continue
            time.sleep(args.intervalo)
        if args.ouvir:
            log.registrar("simulador.ouvindo", f"Ouvindo comandos por {args.ouvir} s")
            time.sleep(args.ouvir)
    finally:
        sentinela.desconectar()
    return 0


def _ouvir(args: argparse.Namespace) -> int:
    sentinela = Sentinela(args.device, args.segredo, args.host, args.porta)
    sentinela.conectar()
    try:
        time.sleep(args.segundos)
    finally:
        sentinela.desconectar()
    return 0


def _comando(args: argparse.Namespace) -> int:
    arquivo = ESTADO_PADRAO / f"{args.device}.core.json"
    try:
        seq = json.loads(arquivo.read_text(encoding="utf-8"))["seq"] + 1
    except FileNotFoundError:
        seq = 1
    msg = publicar_comando(args.device, args.segredo, args.tipo, args.mensagem, seq, args.host, args.porta)
    arquivo.parent.mkdir(parents=True, exist_ok=True)
    arquivo.write_text(json.dumps({"seq": seq}), encoding="utf-8")
    log.registrar(
        "comando.publicado", f"{msg['tipo']}: {msg['mensagem']}", correlation_id=f"{args.device}:c{seq}", seq=seq
    )
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="sentinela-sim", description="Simulador da Sentinela do Sombra")
    sub = parser.add_subparsers(dest="comando", required=True)

    rodar = sub.add_parser("rodar", help="executa um cenário")
    _comum(rodar)
    rodar.add_argument("--cenario", choices=sorted(CENARIOS), required=True)
    rodar.add_argument("--intervalo", type=float, default=10.0, help="segundos entre mensagens (contrato: 10)")
    rodar.add_argument("--ouvir", type=float, default=0.0, help="segundos ouvindo comandos depois do cenário")
    rodar.set_defaults(funcao=_rodar)

    ouvir = sub.add_parser("ouvir", help="só ouve e verifica comandos")
    _comum(ouvir)
    ouvir.add_argument("--segundos", type=float, default=60.0)
    ouvir.set_defaults(funcao=_ouvir)

    comando = sub.add_parser("comando", help="publica um comando assinado, como o Core faria")
    _comum(comando)
    tipos = ["PRE_ALERTA", "ALERTA", "FAMILIA_AVISADA", "LEMBRETE_AGUA", "CHECKIN_DIARIO", "NORMAL"]
    comando.add_argument("--tipo", required=True, choices=tipos)
    comando.add_argument("--mensagem", required=True)
    comando.set_defaults(funcao=_comando)

    lista = sub.add_parser("cenarios", help="lista os cenários")
    lista.set_defaults(
        funcao=lambda _a: print("\n".join(f"{n:26s} {d}" for n, (_, d) in sorted(CENARIOS.items()))) or 0
    )

    args = parser.parse_args(argv)
    if args.comando != "cenarios" and len(getattr(args, "segredo", "")) != 64:
        print("segredo ausente ou inválido: rode `make env` ou passe --segredo com 64 dígitos hex", file=sys.stderr)
        return 2
    return args.funcao(args)


if __name__ == "__main__":
    sys.exit(main())
