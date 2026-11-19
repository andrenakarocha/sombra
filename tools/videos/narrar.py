# /// script
# requires-python = ">=3.10"
# dependencies = ["kokoro-onnx>=0.4", "soundfile"]
# ///
"""Narra as falas de um roteiro com o Kokoro (voz sintética pt-BR, local, sem chave).

Uso: uv run -q tools/videos/narrar.py <falas.json> <pasta-de-saída>
O JSON: {"voz": "pf_dora", "velocidade": 1.0, "cenas": [{"id": "abertura", "fala": "..."}]}
Grava <id>.wav por cena (refaz só a cena cuja fala mudou) e imprime {"id": segundos} no stdout.
"""

import hashlib
import json
import os
import re
import sys
import urllib.request
from pathlib import Path

import numpy as np
import soundfile as sf
from kokoro_onnx import Kokoro

PASTA_MODELO = Path(os.environ.get("SOMBRA_KOKORO_DIR", Path.home() / ".cache/hyperframes/tts"))
MODELO = PASTA_MODELO / "models/kokoro-v1.0.onnx"
VOZES = PASTA_MODELO / "voices/voices-v1.0.bin"
URL = "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/"

# A legenda mostra a grafia certa; a voz recebe como se fala. Siglas lidas letra a letra soam erradas no espeak.
PRONUNCIA = {
    r"\bMQTT\b": "ême quê tê tê",
    r"\bESP32\b": "ê ésse pê trinta e dois",
    r"\bLGPD\b": "éle gê pê dê",
    r"\bPII\b": "pê í í",
    r"\bIA\b": "í á",
    r"\bAPI\b": "á pê í",
    r"\bSignalR\b": "Signal érre",
    r"\bHMAC\b": "agá mac",
    r"\bSHA256\b": "xá dois cinco seis",
    r"\bJSON\b": "jêison",
    r"\bDNS\b": "dê ene ésse",
    r"\bIPs?\b": "í pê",
    r"\bCPU\b": "cê pê u",
    r"\bVM\b": "vê ême",
    r"\bCPF\b": "cê pê éfe",
    r"\bSUS\b": "sús",
    r"\bUBS\b": "u bê ésse",
    r"\bC#": "cê xárp",
    r"\b\.NET\b": "dót nét",
    r"\bML\b": "ême éle",
    r"\bONNX\b": "ónix",
    r"\bXGBoost\b": "xis gê búst",
    r"\bINMET\b": "inmét",
    r"\bWokwi\b": "uóqui",
    r"\bDHT22\b": "dê agá tê vinte e dois",
    r"\bSemgrep\b": "sem grép",
    r"\btaint\b": "têint",
    r"\bGemini\b": "djêmini",
    r"\bdashboard\b": "déshbórd",
    r"\bREADME\b": "rídmi",
    r"\bsystemd\b": "sístem dê",
    r"\bwatchdog\b": "uótch dóg",
    r"\bBash\b": "béxi",
    r"\bnetplan\b": "nét plan",
    r"\bdnsmasq\b": "dê ene ésse másk",
    r"\bstress-ng\b": "stréss ene gê",
    r"\bauditd\b": "ódit dê",
    r"\bkill -9\b": "kill menos nove",
    r"\bPR-AUC\b": "pê érre á u cê",
    r"\brecall\b": "ricól",
    r"\bExpo\b": "Éxpo",
    r"\bReact Native\b": "riéct nêitiv",
    r"\bTalkBack\b": "tóc béc",
    r"\bcheck-ins?\b": "tchéquin",
    r"\bCore\b": "cór",
    r"\bVagrant\b": "vêigrant",
    r"\bVagrantfile\b": "vêigrant fáil",
    r"\bUbuntu\b": "ubúntu",
    r"\bnginx\b": "éngine éxis",
    r"\bSpring\b": "spríng",
    r"\bfeed\b": "fíd",
    r"\btemplate\b": "témpleit",
    r"\bprompt\b": "prómpt",
    r"\bsink\b": "sínk",
    r"\bbroker\b": "brôuker",
    r"\bfirmware\b": "fírmuér",
    r"\bsetpoint\b": "sét póint",
    r"\bnotebook\b": "nôutbuk",
    r"\bpipeline\b": "páiplain",
    r"\bapp\b": "ép",
    r"°C": " graus",
    r"\bNWS\b": "ene dábliu ésse",
    r"\bA701\b": "á sete zero um",
    r"\bA771\b": "á sete sete um",
}


def para_fala(texto: str) -> str:
    for padrao, fala in PRONUNCIA.items():
        texto = re.sub(padrao, fala, texto)
    return re.sub(r"[`*_]", "", texto)


def sintetizar(kokoro: Kokoro, texto: str, voz: str, velocidade: float):
    """Uma frase por vez (o Kokoro tem limite de tokens por chamada), com uma pausa curta entre elas."""
    pedacos, taxa = [], 24000
    for frase in filter(None, (f.strip() for f in re.split(r"(?<=[.!?…:;])\s+", texto))):
        amostras, taxa = kokoro.create(frase, voice=voz, speed=velocidade, lang="pt-br")
        pedacos.extend([amostras, np.zeros(int(taxa * 0.22), dtype=amostras.dtype)])
    return np.concatenate(pedacos), taxa


def baixar_modelo() -> None:
    for destino in (MODELO, VOZES):
        if not destino.exists():
            destino.parent.mkdir(parents=True, exist_ok=True)
            print(f"baixando {destino.name}…", file=sys.stderr)
            urllib.request.urlretrieve(URL + destino.name, destino)


def main() -> None:
    falas = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    saida = Path(sys.argv[2])
    saida.mkdir(parents=True, exist_ok=True)
    voz, velocidade = falas.get("voz", "pf_dora"), float(falas.get("velocidade", 1.0))
    baixar_modelo()
    kokoro = None
    duracoes = {}
    for cena in falas["cenas"]:
        texto = para_fala(cena["fala"])
        marca = hashlib.sha256(f"{voz}|{velocidade}|{texto}".encode()).hexdigest()[:16]
        wav, selo = saida / f"{cena['id']}.wav", saida / f"{cena['id']}.marca"
        if not (wav.exists() and selo.exists() and selo.read_text() == marca):
            kokoro = kokoro or Kokoro(str(MODELO), str(VOZES))
            sf.write(wav, *sintetizar(kokoro, texto, voz, velocidade))
            selo.write_text(marca)
        info = sf.info(wav)
        duracoes[cena["id"]] = round(info.frames / info.samplerate, 3)
    print(json.dumps(duracoes))


if __name__ == "__main__":
    main()
