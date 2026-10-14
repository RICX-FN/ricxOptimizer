# ⚡ ricxOptimizer - Suíte Especialista de Otimização de SO para Jogos

Desenvolvido para Windows 10 e Windows 11 com arquitetura de baixo nível em C# / Win32 API. O **ricxOptimizer** foi projetado para eliminar todos os gargalos, micro-travamentos (*stutters*), latência de entrada (*input lag*) e serviços desnecessários que tornam o Windows pesado para jogos.

---

## 🎯 Visão Geral dos 3 Modos

O aplicativo apresenta uma interface moderna, dark gamer e responsiva com 3 opções claras de perfil e um sistema de backup prévio:

### 1. 🛡️ Windows Default (Restaurar Padrão)
* **Objetivo:** Retornar o sistema ao estado operacional padrão ou original.
* **Mecanismo:**
  * Se um backup foi criado anteriormente pelo usuário, ele restaura os arquivos exatos de backup (Registro, BCD, Serviços originais e Plano de energia).
  * Se nenhum backup foi criado, aplica o perfil *baseline* oficial de fábrica do Windows.
  * Reativa: Windows Defender, Windows Update, Windows Search (Indexação), SysMain, Print Spooler, MMCSS balanceado e plano de energia Equilibrado.

### 2. 🎮 Windows Gaming Mode (Equilibrado para Jogos)
* **Objetivo:** Otimização agressiva mantendo estabilidade para o uso diário (navegação, impressora, loja, áudio).
* **Otimizações aplicadas:**
  * **Energia:** Ativa o plano de energia oculto **Desempenho Máximo (Ultimate Performance)** e desativa o *Core Parking* (desbloqueia todos os núcleos da CPU para ficarem em 100% ativos).
  * **MMCSS (Multimedia Class Scheduler):** Prioriza a fila de jogos (`SystemResponsiveness = 10%`, categoria `High`, prioridade de GPU `8`).
  * **Agendador de Processos:** Ajusta `Win32PrioritySeparation = 0x26` (quantum curto e dinâmico, fornecendo boost de clock máximo para o processo do jogo em primeiro plano).
  * **GameDVR & Game Bar:** Desativa gravação e codificação contínua em segundo plano no registro (`AllowGameDVR = 0`, `AppCaptureEnabled = 0`), liberando memória de vídeo (VRAM) e encoder da GPU.
  * **GPU & Vídeo:** Ativa **HAGS** (*Hardware-Accelerated GPU Scheduling*) e Modo de Jogo.
  * **Latência de Entrada (Mouse):** Desativa a precisão aprimorada do ponteiro (aceleração do Windows), garantindo mira 1:1 e memória muscular pura em FPS.
  * **Kernel & Memória:** Ativa `DisablePagingExecutive = 1` (impede que o Windows faça paginação do Kernel e drivers em disco, mantendo-os 100% na memória RAM).
  * **Rede:** Desativa o algoritmo de Nagle (`TcpAckFrequency = 1` e `TCPNoDelay = 1`) em todos os adaptadores de rede, desativa limitação de pacotes multimídia (`NetworkThrottlingIndex = 0xFFFFFFFF`) e otimiza o *Auto-Tuning*.
  * **Telemetria Básica:** Desativa `DiagTrack` e `WerSvc`.

### 3. ⚡ Windows Gaming Ultra (100% Gamer Extremo)
* **Objetivo:** Modo "sem concessões", ideal para testes em **Máquina Virtual (VM)**, benchmarks extremos e jogos competitivos de alto nível.
* **Otimizações adicionais:**
  * **Segurança e Antivírus:** Desativa o monitoramento em tempo real do **Windows Defender**, proteção comportamental, envio de amostras na nuvem e **SmartScreen** (elimina os constantes ganchos de I/O em `CreateProcess` e leitura de arquivos).
  * **VBS / HVCI (Integridade de Memória):** Desliga o Hypervisor no boot (`bcdedit /set hypervisorlaunchtype off`) e desativa o `DeviceGuard`. No Windows 11, o VBS gera uma **perda de 10% a 25% de FPS**.
  * **Mitigações de CPU (Spectre & Meltdown):** Desativa os patches de mitigação de execução especulativa (`FeatureSettingsOverride = 3`), restaurando a velocidade e taxa de instruções por ciclo (IPC) original do processador.
  * **Timers de Alta Precisão & Micro-stuttering:**
    * BCD: `useplatformtick yes`, `disabledynamictick yes`, `useplatformclock no`.
    * Força a resolução do timer do Windows para **0.5ms (5000 unidades de 100ns)** via `NtSetTimerResolution`.
  * **Debloat Profundo de Serviços:**
    * `SysMain` (Superfetch) -> Desativado e Encerrado (elimina picos de 100% de uso de disco).
    * `WSearch` (Windows Search Indexer) -> Desativado (economiza ciclos de CPU e desgaste de SSD).
    * `DPS`, `WdiServiceHost`, `WdiSystemHost` -> Desativados.
    * `DoSvc` (Delivery Optimization) -> Desativado.
    * `wuauserv`, `UsoSvc`, `BITS` (Windows Update) -> Congelados para que o Windows não baixe atualizações em segundo plano enquanto você joga.
    * `spoolsv` (Spooler de Impressão), `Fax`, `RemoteRegistry`, `WbioSrvc` (Biometria) -> Desativados.
    * Serviços de telemetria do Xbox em segundo plano desativados.
  * **Compressão de Memória:** Executa `Disable-MMAgent -mc -pc` (a compressão e descompressão em tempo real de páginas de RAM causa quedas repentinas de 0.1% e 1% de FPS em combates intensos).
  * **Limpeza da Standby List:** Limpa o cache preso de memória RAM em standby via `NtSetSystemInformation`.
  * **Efeitos Visuais e DWM:** Desativa animações de janela, minimização e efeito de transparência acrílica da área de trabalho.

---

## 💾 Sistema de Backup & Restauração Completo

Antes de qualquer alteração sensível, o `ricxOptimizer` permite ou executa a criação de um backup abrangente contendo:
1. **Ponto de Restauração do Sistema (VSS):** Cria o ponto nomeado `ricxOptimizer_SafetyBackup`.
2. **Exportação de Chaves Críticas do Registro:** Salva todas as chaves modificadas em formato `.reg` nativo.
3. **Snapshot dos Serviços:** Salva o tipo de inicialização (*Auto*, *Demand*, *Disabled*) e status de cada serviço modificado em arquivo estruturado.
4. **Backup das Configurações do BCD:** Exporta o estado atual dos sinalizadores de inicialização do Windows via `bcdedit /export`.
5. **Plano de Energia:** Salva o GUID do plano de energia atualmente em uso.

Ao clicar em **"Restaurar Windows Padrão"**, todos esses componentes são restaurados instantaneamente.

---

## 🚀 Como Executar na Máquina Virtual ou no Windows

Você possui 3 formas imediatas de iniciar o aplicativo:

### Opção 1: Execução em 1 Clique (Recomendado)
Dê um duplo clique no arquivo:
```cmd
run_ricxOptimizer.bat
```
* O script solicitará automaticamente privilégios de Administrador.
* Se o binário `ricxOptimizer.exe` já existir, ele o executará diretamente.
* Se ainda não foi compilado, ele compila automaticamente em 2 segundos usando o `csc.exe` nativo do Windows e inicia a interface!

### Opção 2: Compilar com o Compilador Nativo do Windows (Sem Instalar Nada)
No Windows, abra o prompt de comando ou dê um clique duplo em:
```cmd
scripts\build_native_csc.bat
```
Ele gerará o executável standalone otimizado `ricxOptimizer.exe`.

### Opção 3: Executar Diretamente via PowerShell
Se preferir rodar a interface sem compilar um binário `.exe`:
1. Abra o PowerShell como Administrador.
2. Execute:
```powershell
Set-ExecutionPolicy Bypass -Scope Process -Force
.\ricxOptimizer.ps1
```

---

## 🛠️ Diagnóstico para Testes em Máquinas Virtuais

Para verificar o ambiente de teste antes e depois de aplicar o modo Ultra Gamer:
```cmd
scripts\test_virtual_machine.bat
```
O script exibirá:
* Fabricante do hipervisor (VMware, VirtualBox, Hyper-V, KVM).
* Estado do BCD (`disabledynamictick`, `hypervisorlaunchtype`).
* Status do VBS / Integridade de Memória.
* Status do Defender.
