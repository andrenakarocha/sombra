# A entrevista: Escutar (etapa 2 da GS)

> Quem convive com o problema: **Dona Cida**, pessoa idosa que mora sozinha em Moema, São Paulo (D20), e **a filha**,
> que cuida dela de longe. A conversa seguiu o roteiro de 8 perguntas da [spec 00](specs/00-fundacao-contratos.md). Ela
> autorizou a gravação e o uso do primeiro nome; o nome da filha virou "a filha" (D14). Nenhum remédio foi anotado, só
> se havia algum. As fotos do território ficam fora do repositório (D14).
>
> A persona do sistema, a **Dona Lurdes**, de 78 anos, é inspirada na Dona Cida, mas é fictícia, como todos os dados de
> pessoas do Sombra.

## O que mudou no projeto por causa dela

| O que ouvimos | O que o Sombra faz por isso | Onde |
|---|---|---|
| "Lá pelas três, quatro horas, parece que a casa virou um forno", e "de noite demora pra refrescar" | a Sentinela mede o calor **de dentro da casa**, não o da rua; o ML prevê as **próximas 24 h** para avisar antes do pico | [`iot/sentinela/`](../iot/sentinela/), [`ml/`](../ml/) |
| "Água eu bebo, mas às vezes esqueço", vendo televisão | **lembrete de água no pico da tarde** (15h e 15h45 nos dias de calor), no máximo 2 por dia | Rotina no app, D13 |
| Passou mal sozinha na cozinha; foi a **vizinha** que percebeu e ligou para a filha | a **rede de cuidado** tem a família e quem está perto (a vizinha), e o alerta **sobe de degrau** até alguém responder | escalonamento no Core, D13 |
| "Se eu tivesse caído sozinha, não sei quem ia perceber" | o **"estou bem"** num botão físico grande, que funciona sem internet | Sentinela, Modo Idoso |
| No celular, "quando aparece muita notificação, eu fico perdida"; prefere "letra grande e sem muita coisa na tela" | o **Modo Idoso**: uma tela só, letra gigante, um botão verde, um vermelho com pressão longa, e a voz lendo o recado | [`mobile/`](../mobile/) |
| "Não pode ficar piscando toda hora, porque aí incomoda" | a luz da Sentinela é **fixa e nunca pisca**; ela não apita sem parar | D13, firmware |
| "Só teria que me explicar direitinho o que acontece se eu esquecer de apertar" | toda tela diz o próximo passo: "Em 15 min: família", "Se não apertar até 15:06, sua família recebe um lembrete" | OLED e Modo Idoso |
| Toma remédio de que o médico mandou ter cuidado no calor | **limites pessoais**: o perigo começa antes para quem toma diurético ou tem cardiopatia (R3) | "Por que ela está em risco" no app |
| A filha: "não queria receber um alerta de emergência toda vez que ela esquecesse de apertar um botão" | antes do alarme vem **um lembrete leve**; alerta de verdade só em perigo ou pedido de ajuda; carência de 1 h por casa; e o indicador **alarmes desnecessários por semana** | D13, Ficha |
| A filha "trabalha fora e não consegue acompanhar o tempo todo" | o **app da família** mostra a casa ao vivo, sem precisar ligar | [`mobile/`](../mobile/) |

## Transcrição

**Entrevistador:** Antes de começarmos, a senhora autoriza que a gente grave esta conversa para estudar formas de ajudar
idosos durante períodos de muito calor? Vamos usar apenas seu primeiro nome.

**Dona Cida:** Pode sim, meu filho. Não tem problema, não. Se for pra ajudar outras pessoas também, melhor ainda.

### 1. Como é o seu dia quando faz muito calor? Em que hora a casa fica mais quente?

**Dona Cida:** Ah, é muito ruim, viu? Eu moro numa casa que pega bastante sol de tarde. Até umas onze horas dá pra
aguentar, mas depois do almoço começa a esquentar muito. Lá pelas três, quatro horas, parece que a casa virou um forno.

Eu fico mais quietinha, porque até pra fazer as coisas dá uma preguiça. E de noite demora pra refrescar, então às vezes
nem consigo dormir direito.

### 2. O que a senhora faz para se refrescar? Lembra de beber água?

**Dona Cida:** Eu ligo o ventilador, abro as janelas e, quando tá muito quente mesmo, molho uma toalhinha e coloco no
pescoço.

Água eu bebo, mas vou te falar que às vezes esqueço, viu? Principalmente quando tô assistindo televisão. Minha filha vive
falando pra eu beber mais água. Agora eu deixo uma garrafinha do meu lado pra lembrar.

### 3. Já passou mal com calor? O que aconteceu e quem ficou sabendo?

**Dona Cida:** Já sim. Teve um dia no verão passado que eu fiquei meio tonta, com uma fraqueza danada. Eu tava na cozinha
e precisei sentar porque achei que ia cair.

Minha vizinha veio aqui, viu que eu não tava muito bem e ligou pra minha filha. Depois minha filha me levou ao médico.
Fiquei com medo, porque se eu tivesse caído sozinha, não sei quem ia perceber.

### 4. Quem costuma ver como a senhora está? Com que frequência?

**Dona Cida:** Minha filha me liga todo dia, geralmente de manhã ou na hora do almoço. Ela trabalha bastante, coitada,
então não consegue vir sempre.

Ela vem aqui umas duas vezes por semana. Tenho uma vizinha que também olha como eu tô de vez em quando. Mas passo a maior
parte do dia sozinha mesmo.

### 5. Usa celular? Para quê? O que é difícil nele?

**Dona Cida:** Uso sim! Tenho meu WhatsApp, converso com minhas amigas, vejo foto dos meus netos e faço chamada de vídeo
de vez em quando.

Mas tem coisa que eu não entendo. Quando aparece muita mensagem ou notificação, eu fico perdida. Às vezes aperto alguma
coisa sem querer e não sei voltar. Prefiro quando é simples, com letra grande e sem muita coisa na tela.

### 6. Se uma luz e um botão grande pedissem "aperte se está bem", isso ajudaria ou incomodaria?

**Dona Cida:** Ah, eu acho que ajudaria, sim. Se fosse um botão bem grande, fácil de enxergar, eu conseguiria apertar.

Agora, não pode ficar piscando toda hora, né? Porque aí incomoda. Se acendesse uma luzinha uma vez por dia, ou quando
tivesse muito calor, pra eu avisar minha filha que tô bem, seria bom.

Só teria que me explicar direitinho o que acontece se eu esquecer de apertar. Não queria deixar minha filha preocupada à
toa.

### 7. Toma algum remédio que o médico disse para ter cuidado no calor?

**Dona Cida:** Sim.

*Registro da pesquisa: sim. Nenhum nome de medicamento coletado.*

### 8. Para quem cuida: como você sabe hoje se ela está bem? O que te dá medo?

**Entrevistador:** Como você costuma saber se sua mãe está bem?

**A filha:** Geralmente eu mando mensagem ou ligo. Se ela não atende, tento de novo depois de um tempo. Mas quando demora
muito, começo a ficar preocupada.

O que mais me dá medo é ela passar mal sozinha, principalmente nesses dias de calor extremo. Eu trabalho fora e não
consigo acompanhar o tempo todo.

Seria muito bom ter alguma maneira simples de saber se ela está bem sem precisar ficar ligando toda hora. Mas também não
queria receber um alerta de emergência toda vez que ela esquecesse de apertar um botão.
