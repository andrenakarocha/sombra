# Spec 08 — Operating Systems · Infra Linux do Sombra

> **Fase:** F8 🔑 (Azure na F11.8, vídeo na F13.6) · **Pasta:** `infra/` · **Checkpoints:** `PLANO.md` §8.9
> **Entrega:** link do GitHub (pasta) + vídeo ≤ 7 min + evidências no repositório · **nomes de todos os integrantes**
> **Ambiente:** VM local (Vagrant + Ubuntu 24.04) e, opcionalmente, **os mesmos scripts** numa VM do Azure (decisão D2)

## Objetivo
A partir de uma VM **vazia**, um único `sudo ./provision.sh` deixa o servidor `sombra-srv01` configurado (hostname, IP, DNS, usuários) e **hospedando o Sombra inteiro** (Core, Clima, painel, Postgres, nginx) como serviços systemd, com **monitoramento como serviço tolerante a falhas**, **logs de auditoria** de tudo e **testes de falha** que provam a recuperação.

## Rubrica → como

| Critério | Pts | Como |
|---|---|---|
| Provisionamento de infra (rede, DNS, IP) | 2 | `steps/10-host.sh` (hostname, fuso, locale) · `20-rede.sh` (IP estático via netplan, parâmetros de rede) · `30-dns.sh` (**dnsmasq** com a zona `sombra.local`: `api`, `clima`, `painel` + systemd-resolved apontando para ele) · `40-usuarios.sh` (usuário de serviço `sombra` sem shell, grupo `sombra-ops`, sudoers mínimo, permissões) |
| Provisionamento da aplicação | 2 | `50-dependencias.sh` (runtime .NET 10, JDK 21, PostgreSQL 16, nginx, jq, stress-ng) · `60-banco.sh` (papéis e bancos por serviço, `pg_hba` restrito) · `70-aplicacao.sh` (baixa o **release** do GitHub e **confere o SHA256**, cria `/opt/sombra`, `/etc/sombra` em modo 0640, `/var/log/sombra`, arquivos de ambiente, units systemd endurecidas, migrações, vhosts do nginx, start automático) |
| Monitor de CPU, disco e memória com alertas | 2 | `monitor/sombra-monitor.sh`: CPU por delta de `/proc/stat`, RAM por `MemAvailable`, disco por `df`, limites WARN/CRIT em `/etc/sombra/monitor.conf`, alerta em log + journald + **`POST /api/v1/ops/alertas` no Core**, que aparece no painel "Operação". Também checa `systemctl is-active` e `/health` dos serviços |
| Monitoramento como serviço tolerante a falhas | 1 | `sombra-monitor.service`: `Type=notify` + `WatchdogSec=30` (o script avisa o systemd a cada ciclo), `Restart=always`, `RestartSec=5`, `StartLimitIntervalSec`/`Burst`, habilitado no boot. `start`, `stop` e `restart` documentados |
| Logs de auditoria e execução | 1 | `lib/log.sh` + `lib/auditoria.sh`: toda etapa registra **o que, quando, quem e o resultado** (código de saída) em `/var/log/sombra/provision.log` + trilha JSON em `auditoria.jsonl`. `trap ERR` registra falhas com linha e comando. logrotate. Regras do `auditd` em `/etc/sombra` e `/opt/sombra` |
| Funcionamento completo | 2 | `steps/90-validacao.sh`: DNS resolve, portas abertas, os três `/health` respondem 200, painel abre por `painel.sombra.local`, roteiro da Dona Lurdes roda contra a VM |

## Estrutura
```
infra/
├── provision.sh              orquestra as etapas em ordem: idempotente, com log, para na primeira falha
├── lib/ (log.sh · auditoria.sh · comum.sh)
├── steps/ 10-host · 20-rede · 30-dns · 40-usuarios · 50-dependencias · 60-banco · 70-aplicacao · 80-monitor · 90-validacao
├── monitor/ sombra-monitor.sh · monitor.conf
├── systemd/ sombra-core.service · sombra-clima.service · sombra-monitor.service
├── nginx/ sombra.conf
├── falhas/ 01-matar-monitor · 02-cpu-alta · 03-memoria-alta · 04-disco-cheio · 05-derrubar-api · 06-reboot · 07-falha-de-script
├── Vagrantfile               (a VM só chama o provision.sh, e o mesmo script roda em qualquer Ubuntu)
├── evidencias/               prints + trechos de log de cada critério
└── README.md
```

## Testes de falha (com evidência)
| # | Simulação | Esperado |
|---|---|---|
| 01 | `kill -9` no monitor | o systemd recupera em ~5 s e registra no journal |
| 02 | `stress-ng --cpu` | alerta CRIT de CPU no log e no painel "Operação" |
| 03 | `stress-ng --vm` | alerta de memória |
| 04 | `fallocate` de um arquivo enorme | alerta de disco |
| 05 | `kill -9` na API | o systemd reinicia, o painel mostra a queda e a volta |
| 06 | `reboot` da VM | todos os serviços voltam sozinhos |
| 07 | etapa de script com erro proposital | `trap` registra linha, comando e código no log de auditoria |

## Checkpoints
A ordem de execução e os 🔎 ficam no `PLANO.md` (fase indicada no topo desta spec).

## Roteiro do vídeo (≤ 7 min)
0:00 a solução e a infra → 0:30 `vagrant up` do zero com os logs rolando (acelerado) → 2:00 hostname, IP, DNS e usuários → 2:45 a aplicação no ar (painel + API) → 3:30 monitor e serviço systemd (status, journal, restart) → 4:30 **testes de falha** (kill, stress, reboot) e recuperação → 6:00 logs de auditoria → 6:40 fechamento.

## Defesa
O que `Restart=always` + `WatchdogSec` garantem · por que um usuário de serviço sem shell · como provar que o script é idempotente · por que conferir o SHA256 do release.
