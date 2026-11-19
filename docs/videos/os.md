# Vídeo de Operating Systems (até 7 min)

> Rubrica: provisionamento de infra (hostname, IP, DNS) **2** · provisionamento da aplicação **2** · monitor de CPU,
> memória e disco com alertas **2** · monitoramento como serviço tolerante a falhas **1** · logs de auditoria e execução
> **1** · funcionamento completo **2**. O mapa critério → script → evidência está no [`infra/README.md`](../../infra/README.md).

## Preparar

```bash
cd infra
scripts/empacotar.sh           # os artefatos do Core, do Clima e do painel com o SHA256SUMS (uma vez)
vagrant destroy -f             # para o vagrant up começar do zero no vídeo
```

Deixe o painel aberto em <http://painel.sombra.local> (no `/etc/hosts` do host: `192.168.56.10 painel.sombra.local
api.sombra.local clima.sombra.local`) para o trecho da aplicação e o do alerta. Na VM, a senha das contas da demo é gerada no
provisionamento: pegue **antes de gravar e fora da tela** com `vagrant ssh -c 'sudo grep DEMO_SENHA /etc/sombra/segredos.env'`.

## Linha do tempo

| Tempo | Tela | Fazer | Fala |
|---|---|---|---|
| 0:00 | slide ou README | — | "O Sombra protege pessoas idosas que moram sozinhas durante ondas de calor. Para a disciplina de Sistemas Operacionais, a gente transformou uma **VM Ubuntu vazia num servidor com o Sombra inteiro no ar, só com scripts Bash**: rede, DNS, usuários, banco, serviços, monitoramento e auditoria." |
| 0:30 | terminal | `vagrant up` | "O Vagrantfile só cria a VM e chama o `provision.sh`. Ele roda nove etapas numeradas, e cada uma registra em JSON o que fez, quando, quem e o resultado." (deixe rolar; no editor, acelere 4×) |
| 2:00 | terminal na VM: `vagrant ssh` | `hostnamectl`, `ip -br a`, `dig api.sombra.local +short`, `id sombra`, `getent passwd sombra`, `sudo -l -U vagrant` | "**Hostname** `sombra-srv01`, **IP estático** 192.168.56.10 pelo netplan, um **DNS próprio** no dnsmasq com a zona `sombra.local`. O usuário de serviço `sombra` não tem shell, e o sudo do operador só deixa mexer nos serviços do Sombra." |
| 2:45 | navegador | abrir o painel; login | "A **aplicação** foi instalada a partir do release, com o **SHA256 conferido**, em `/opt/sombra/releases`, com os segredos gerados na própria VM e um banco por serviço. O painel, a API e o serviço de clima respondem pelos nomes do DNS." |
| 3:30 | terminal na VM | `systemctl status sombra-monitor`, `journalctl -u sombra-monitor -n 5`, `cat /etc/sombra/monitor.conf` | "O **monitor** de CPU, memória, disco e serviços é um **serviço systemd** do tipo notify com **watchdog**: se ele travar ou morrer, o systemd sobe de novo. Três medições seguidas acima do limite viram alerta." |
| 4:30 | terminal na VM | `sudo /vagrant/falhas/01-matar-monitor.sh` e `sudo /vagrant/falhas/02-cpu-alta.sh` (ou os prints de `infra/evidencias/`) | "Os **testes de falha**: `kill -9` no monitor, e ele volta sozinho em 5 segundos. CPU a 100% com o stress-ng: o alerta sai no log, no journal e chega ao painel." (mostre o alerta em Operação no painel) |
| 5:30 | terminal na VM | `sudo /vagrant/falhas/06-reboot.sh` (a VM reinicia); depois `vagrant ssh` e `sudo /vagrant/falhas/06-reboot.sh --conferir` | "Depois de um **reboot**, os sete serviços voltam sozinhos." |
| 6:00 | terminal na VM | `sudo tail -n 5 /var/log/sombra/auditoria.jsonl \| jq .` e `sudo ausearch -k sombra-config \| tail` | "A **trilha de auditoria**: cada ação com o quê, quando, quem e o resultado. O `trap` de erro registra linha, comando e código de qualquer falha, e o auditd vigia as configurações." |
| 6:30 | terminal | `vagrant provision` | "E a prova de que é reproduzível: uma segunda execução **não muda nada** — zero arquivos gravados." |
| 6:45 | README | — | "Tudo está no `infra/README.md`, com um print para cada critério. Integrantes: André, João e Matheus." |

> Se faltar tempo, troque os testes ao vivo pelos prints em `infra/evidencias/` (os 7 testes já rodaram e estão
> registrados) e mostre só o `kill -9` ao vivo. A pasta `infra/` do repositório aparece na VM em `/vagrant`.
