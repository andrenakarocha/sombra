# /// script
# requires-python = ">=3.12"
# dependencies = ["pyyaml>=6"]
# ///
"""Testa o `sincronizar_azure.py` contra um Azure DevOps falso (um servidor HTTP local que guarda os work items como o
Azure guarda): a primeira execução cria o Epic, as Features e os Test Cases com os vínculos e os passos; a segunda não
cria nem muda nada (P12); mudar um caso no YAML muda só ele.

Uso (na raiz): uv run qa/ferramentas/testar_sincronizacao.py
"""

from __future__ import annotations

import base64
import copy
import json
import sys
import threading
import unittest
import xml.etree.ElementTree as ET
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

import yaml

sys.path.insert(0, str(Path(__file__).resolve().parent))
import sincronizar_azure as s  # noqa: E402

PAT = "pat-de-teste"


class AzureFalso(BaseHTTPRequestHandler):
    itens: dict[int, dict] = {}
    proximo = 100
    autorizacoes: set[str] = set()

    def log_message(self, *_):  # silencioso
        pass

    def _responder(self, codigo: int, corpo: dict) -> None:
        dados = json.dumps(corpo).encode()
        self.send_response(codigo)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(dados)))
        self.end_headers()
        self.wfile.write(dados)

    def _corpo(self) -> list:
        return json.loads(self.rfile.read(int(self.headers["Content-Length"])))

    def _aplicar(self, item: dict, operacoes: list) -> None:
        for op in operacoes:
            assert op["op"] == "add", op
            if op["path"].startswith("/fields/"):
                item["fields"][op["path"].removeprefix("/fields/")] = op["value"]
            elif op["path"] == "/relations/-":
                item["relations"].append(op["value"])

    def do_POST(self):  # noqa: N802
        AzureFalso.autorizacoes.add(self.headers["Authorization"])
        tipo = self.path.split("/$", 1)[1].split("?", 1)[0].replace("%20", " ")
        AzureFalso.proximo += 1
        item = {"id": AzureFalso.proximo, "fields": {"System.WorkItemType": tipo}, "relations": []}
        self._aplicar(item, self._corpo())
        AzureFalso.itens[item["id"]] = item
        self._responder(200, item)

    def _item(self) -> dict | None:
        return AzureFalso.itens.get(int(self.path.split("/workitems/", 1)[1].split("?", 1)[0]))

    def do_GET(self):  # noqa: N802
        item = self._item()
        self._responder(200, item) if item else self._responder(404, {"message": "não existe"})

    def do_PATCH(self):  # noqa: N802
        item = self._item()
        self._aplicar(item, self._corpo())
        self._responder(200, item)


class Sincronizacao(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.servidor = HTTPServer(("127.0.0.1", 0), AzureFalso)
        threading.Thread(target=cls.servidor.serve_forever, daemon=True).start()
        org = f"http://127.0.0.1:{cls.servidor.server_port}/sombra-gs"
        cls.org = org
        cls.features = yaml.safe_load((s.ESPEC / "features.yaml").read_text(encoding="utf-8"))
        cls.casos = yaml.safe_load((s.ESPEC / "casos-de-teste.yaml").read_text(encoding="utf-8"))

    @classmethod
    def tearDownClass(cls):
        cls.servidor.shutdown()

    def setUp(self):
        AzureFalso.itens.clear()

    def azure(self) -> s.Azure:
        return s.Azure(self.org, "Sombra GS", PAT)

    def test_cria_tudo_com_vinculos_e_passos_e_a_segunda_execucao_nao_muda_nada(self):
        mapa: dict = {}
        primeira = s.sincronizar(self.azure(), self.features, self.casos, mapa)
        n_features, n_casos = len(self.features["features"]), len(self.casos["casos"])
        self.assertEqual(primeira["criados"], 1 + n_features + n_casos)
        self.assertEqual(len(AzureFalso.itens), 1 + n_features + n_casos)

        tipos = sorted(i["fields"]["System.WorkItemType"] for i in AzureFalso.itens.values())
        self.assertEqual(tipos.count("Feature"), n_features)
        self.assertEqual(tipos.count("Test Case"), n_casos)
        for f in self.features["features"]:
            item = AzureFalso.itens[mapa["features"][f["id"]]]
            self.assertEqual(item["relations"][0]["rel"], s.PAI)
            self.assertTrue(item["relations"][0]["url"].endswith(f"/{mapa['epico']}"))
            self.assertIn("Dado", item["fields"]["Microsoft.VSTS.Common.AcceptanceCriteria"])
        for c in self.casos["casos"]:
            item = AzureFalso.itens[mapa["casos"][c["id"]]]
            self.assertEqual(item["relations"], [{"rel": s.TESTA, "url": self.azure().url_do_item(mapa["features"][c["feature"]])}])
            passos = ET.fromstring(item["fields"]["Microsoft.VSTS.TCM.Steps"]).findall("step")
            self.assertEqual(len(passos), len(c["passos"]))
            self.assertIn(str(c["passos"][0]["acao"]).split()[0], passos[0][0].text)

        segunda = s.sincronizar(self.azure(), self.features, self.casos, mapa)
        self.assertEqual(segunda, {"criados": 0, "atualizados": 0, "iguais": 1 + n_features + n_casos})
        self.assertEqual(len(AzureFalso.itens), 1 + n_features + n_casos)

    def test_mudar_um_caso_atualiza_so_ele_sem_duplicar_o_vinculo(self):
        mapa: dict = {}
        s.sincronizar(self.azure(), self.features, self.casos, mapa)
        casos = copy.deepcopy(self.casos)
        casos["casos"][0]["titulo"] += " (revisado)"

        resultado = s.sincronizar(self.azure(), self.features, casos, mapa)

        self.assertEqual((resultado["criados"], resultado["atualizados"]), (0, 1))
        item = AzureFalso.itens[mapa["casos"][casos["casos"][0]["id"]]]
        self.assertTrue(item["fields"]["System.Title"].endswith("(revisado)"))
        self.assertEqual(len(item["relations"]), 1)

    def test_o_pat_vai_so_no_header_basic(self):
        s.sincronizar(self.azure(), self.features, {"casos": []}, {})
        esperado = "Basic " + base64.b64encode(f":{PAT}".encode()).decode()
        self.assertEqual(AzureFalso.autorizacoes, {esperado})

    def test_simular_nao_chama_ninguem(self):
        contagem = s.sincronizar(None, self.features, self.casos, {})
        self.assertEqual(contagem["criados"], 1 + len(self.features["features"]) + len(self.casos["casos"]))
        self.assertEqual(AzureFalso.itens, {})


if __name__ == "__main__":
    unittest.main(verbosity=2)
