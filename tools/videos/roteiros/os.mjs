// Vídeo de Operating Systems (docs/videos/os.md), até 7 min: a VM vazia vira o sombra-srv01 só com scripts Bash.
import { spawnSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';

const VM = 'vagrant@sombra-srv01:~$';
const PAINEL = 'http://painel.sombra.local';
let INFRA;
let SSH_CFG;

const aspas = (s) => `'${s.replace(/'/g, `'\\''`)}'`;
/** Roda o comando dentro da VM (ssh com a config do Vagrant) e mostra como se fosse digitado no shell dela. */
const vm = (ctx, cmd, o = {}) => ctx.rodar(`ssh -F ${SSH_CFG} -o LogLevel=ERROR default ${aspas(cmd)}`, { mostrar: cmd, prompt: VM, ...o });
/** Vários comandos na VM, espalhados pela fala da cena. */
async function naVm(ctx, comandos, reserva = 1) {
  for (const [i, c] of comandos.entries()) {
    await vm(ctx, c);
    await ctx.esperar(Math.max(0.8, (ctx.restante() - reserva) / (comandos.length - i)));
  }
}

export default {
  nome: 'os',
  limite: 420,
  voz: 'pf_dora',
  chromeArgs: ['--host-resolver-rules=MAP painel.sombra.local 192.168.56.10, MAP api.sombra.local 192.168.56.10, MAP clima.sombra.local 192.168.56.10'],

  async preparar(ctx) {
    INFRA = join(ctx.raiz, 'infra');
    SSH_CFG = join(ctx.raiz, 'tools/videos/saida/os/ssh.cfg');
    await ctx.slide({
      titulo: 'Uma VM vazia vira o servidor do Sombra',
      sub: 'Operating Systems · Ubuntu 24.04 provisionado só com scripts Bash',
      itens: ['Rede, DNS e usuários', 'A aplicação a partir do release', 'Monitor como serviço tolerante a falhas', 'Auditoria de cada ação'],
    });
  },

  cenas: [
    {
      id: 'abertura',
      tela: 'palco',
      fala: 'O Sombra protege pessoas idosas que moram sozinhas durante ondas de calor. Para a disciplina de Sistemas Operacionais, a gente transformou uma VM Ubuntu vazia num servidor com o Sombra inteiro no ar, só com scripts Bash: rede, DNS, usuários, banco, serviços, monitoramento e auditoria.',
      async acao() {},
    },
    {
      id: 'vagrant-up',
      tela: 'palco',
      acelerar: 4,
      fala: 'Começo do zero: destruo a VM e subo de novo. O Vagrantfile só cria a máquina e chama o provision.sh, que roda nove etapas numeradas: host, rede, DNS, usuários, dependências, banco, aplicação, monitor e validação. Cada etapa registra em JSON o que fez, quando, quem fez e o resultado. Aqui, acelerado quatro vezes.',
      async acao(ctx) {
        await ctx.terminal({ titulo: 'operador@sombra: ~/sombra/infra' });
        await ctx.selo('⏩ acelerado 4×');
        await ctx.rodar('vagrant destroy -f && vagrant up', { cwd: INFRA, timeout: 900 });
        await ctx.selo('');
        const cfg = spawnSync('vagrant', ['ssh-config'], { cwd: INFRA, encoding: 'utf8' });
        writeFileSync(SSH_CFG, cfg.stdout);
        const segredo = spawnSync('ssh', ['-F', SSH_CFG, '-o', 'LogLevel=ERROR', 'default', 'sudo grep -h DEMO_SENHA /etc/sombra/segredos.env'], { encoding: 'utf8' });
        ctx.dados.senha = (segredo.stdout.match(/DEMO_SENHA=(.*)/) ?? [])[1]?.trim().replace(/^["']|["']$/g, '');
      },
    },
    {
      id: 'rede',
      tela: 'palco',
      fala: 'Dentro da VM. O hostname é sombra-srv01. O IP estático, 192.168.56.10, vem do netplan. O DNS é próprio, no dnsmasq, com a zona sombra.local. O usuário de serviço sombra não tem shell de login, e o grupo dos operadores, sombra-ops, só tem sudo para mexer nos serviços do Sombra.',
      async acao(ctx) {
        await ctx.terminal({ titulo: 'vagrant@sombra-srv01: ~' });
        await naVm(ctx, ['hostnamectl --static', 'ip -br -4 a', 'dig +short api.sombra.local', 'getent passwd sombra', 'sudo grep -v "^#" /etc/sudoers.d/sombra-ops']);
      },
    },
    {
      id: 'aplicacao',
      tela: 'palco',
      fala: 'A aplicação foi instalada a partir do release, com o SHA256 conferido antes, em /opt/sombra/releases, com o link "atual" apontando para a versão. Os segredos são gerados na própria VM, e cada serviço tem o seu banco. Os sete serviços estão ativos.',
      async acao(ctx) {
        await ctx.terminal({ titulo: 'vagrant@sombra-srv01: ~', limpar: false });
        await naVm(ctx, [
          'ls -l /opt/sombra/atual',
          `sudo grep '"release:' /var/log/sombra/auditoria.jsonl | tail -n 1 | jq -c 'del(.uid, .host, .servico, .correlationId)'`,
          'systemctl is-active postgresql mosquitto nginx dnsmasq sombra-clima sombra-core sombra-monitor | paste -sd " "',
        ]);
      },
    },
    {
      id: 'painel',
      tela: 'painel',
      fala: 'E o painel, a API e o serviço de clima respondem pelos nomes do DNS. Entro no painel, servido pelo nginx da VM, com a senha da demo gerada no provisionamento. Ele fica ao vivo, conectado ao Core da própria máquina. Em Operação, aparecem a saúde de cada serviço e os alertas do monitor.',
      async acao(ctx) {
        const p = ctx.painel;
        await p.goto(`${PAINEL}/entrar`);
        await ctx.clicar(p.getByLabel('E-mail'));
        await p.getByLabel('E-mail').pressSequentially('operador@demo.sombra', { delay: 45 });
        await ctx.clicar(p.getByLabel('Senha'));
        await p.getByLabel('Senha').pressSequentially(ctx.dados.senha ?? '', { delay: 45 });
        await ctx.clicar(p.getByRole('button', { name: 'Entrar' }));
        await p.getByRole('heading', { name: 'Visão geral' }).waitFor({ timeout: 20000 });
        await p.getByText('Ao vivo', { exact: true }).waitFor({ timeout: 30000 });
        await p.waitForLoadState('networkidle').catch(() => {});
        await p.mouse.move(700, 450, { steps: 30 });
        await ctx.esperar(Math.max(1.5, ctx.restante() - 3));
        await ctx.clicar(p.getByRole('link', { name: 'Operação' }));
        await ctx.esperar(2);
      },
    },
    {
      id: 'monitor',
      tela: 'palco',
      fala: 'O monitor de CPU, memória, disco e serviços é um serviço systemd do tipo notify, com watchdog: se ele travar ou morrer, o systemd sobe de novo. Os limites ficam num arquivo de configuração, e três medições seguidas acima do limite viram um alerta.',
      async acao(ctx) {
        await ctx.terminal({ titulo: 'vagrant@sombra-srv01: ~' });
        await naVm(ctx, [
          'systemctl show sombra-monitor -p Type -p Restart -p WatchdogUSec -p ActiveState',
          'journalctl -u sombra-monitor -n 3 --no-pager -o cat | cut -c1-150',
          'grep -vE "^(#|$)" /etc/sombra/monitor.conf | head -n 8',
        ]);
      },
    },
    {
      id: 'matar-monitor',
      tela: 'palco',
      fala: 'Agora, os testes de falha. Primeiro, kill -9 no monitor: o systemd percebe e ele volta sozinho, com outro PID, em cerca de cinco segundos.',
      async acao(ctx) {
        await ctx.terminal({ titulo: 'vagrant@sombra-srv01: ~' });
        await vm(ctx, 'sudo /vagrant/falhas/01-matar-monitor.sh');
      },
    },
    {
      id: 'cpu-alta',
      tela: 'palco',
      acelerar: 4,
      fala: 'Depois, CPU a cem por cento com o stress-ng. O monitor mede, confirma em três leituras e alerta: no log, no journal e no Core, que empurra o alerta para o painel. Acelerado quatro vezes.',
      async acao(ctx) {
        await ctx.selo('⏩ acelerado 4×');
        await vm(ctx, 'sudo /vagrant/falhas/02-cpu-alta.sh', { timeout: 200 });
        await ctx.selo('');
      },
    },
    {
      id: 'alerta-no-painel',
      tela: 'painel',
      fala: 'No painel, em Operação, o alerta de CPU chegou ao vivo, sem recarregar a página.',
      async acao(ctx) {
        const p = ctx.painel;
        const cartao = p.getByText('Alertas de infraestrutura');
        await cartao.scrollIntoViewIfNeeded();
        const c = await cartao.boundingBox();
        if (c) await p.mouse.move(c.x + 200, c.y + 80, { steps: 30 });
      },
    },
    {
      id: 'reboot',
      tela: 'palco',
      acelerar: 4,
      fala: 'E um reboot da VM inteira. Quando ela volta, os sete serviços sobem sozinhos, na ordem certa, sem ninguém tocar em nada.',
      async acao(ctx) {
        await ctx.terminal({ titulo: 'vagrant@sombra-srv01: ~' });
        await ctx.selo('⏩ acelerado 4×');
        await vm(ctx, 'sudo /vagrant/falhas/06-reboot.sh', { timeout: 60 });
        await ctx.esperar(25);
        for (let i = 0; i < 30; i++) {
          const r = spawnSync('ssh', ['-F', SSH_CFG, '-o', 'LogLevel=ERROR', '-o', 'ConnectTimeout=3', 'default', 'true']);
          if (r.status === 0) break;
          await ctx.esperar(3);
        }
        await vm(ctx, 'sudo /vagrant/falhas/06-reboot.sh --conferir', { timeout: 240 });
        await ctx.selo('');
      },
    },
    {
      id: 'auditoria',
      tela: 'palco',
      fala: 'A trilha de auditoria: cada ação, com o quê, quando, quem e o resultado, numa linha JSON. O auditd vigia as configurações do Sombra. E o trap de erro: aqui, uma etapa de teste tenta copiar um arquivo que não existe. O provisionamento para ali, registra a linha, o comando e o código de saída, e a etapa seguinte não roda.',
      async acao(ctx) {
        await ctx.terminal({ titulo: 'vagrant@sombra-srv01: ~' });
        await naVm(ctx, [
          `sudo tail -n 4 /var/log/sombra/auditoria.jsonl | jq -c '{quando, quem, acao, resultado}'`,
          'sudo ausearch --input-logs -k sombra-config -i 2>/dev/null | grep "type=SYSCALL" | tail -n 2 | cut -c1-170',
          'sudo /vagrant/falhas/07-falha-de-script.sh',
        ], 0.5);
      },
    },
    {
      id: 'idempotente',
      tela: 'palco',
      fala: 'E a prova de que é reproduzível: uma segunda execução do provisionamento, sobre a máquina pronta, não muda nada. Contando os eventos do log, as nove etapas concluem, a validação é aprovada, e não aparece nenhum config.alterada: zero arquivos gravados.',
      async acao(ctx) {
        await ctx.terminal({ titulo: 'operador@sombra: ~/sombra/infra' });
        const aqui = join(ctx.raiz, 'tools/videos/saida/os');
        await ctx.rodar('vagrant provision > segunda-execucao.log 2>&1', { cwd: aqui, env: { VAGRANT_CWD: INFRA }, timeout: 300 });
        await ctx.rodar(`sed 's/^ *default: //' segunda-execucao.log | jq -rR 'fromjson? | .event' | sort | uniq -c | sort -rn`, { cwd: aqui });
      },
    },
    {
      id: 'fechamento',
      tela: 'palco',
      fala: 'Tudo isso está no README da pasta infra, com um print de evidência para cada critério e os sete testes de falha. Integrantes: André, João e Matheus. Obrigado!',
      folga: 1.2,
      async acao(ctx) {
        await ctx.slide({
          titulo: 'infra/README.md',
          sub: 'Cada critério com o script e a evidência',
          itens: ['9 etapas idempotentes, validação de 26 pontos', 'Monitor Type=notify com watchdog', '7 testes de falha com print', 'Reprodução do zero provada'],
        });
      },
    },
  ],
};
