# BODYCAM Open World — v0.1 (Unity 2022.3 LTS + URP)

Jogo 3D de mundo aberto em primeira pessoa com visual de câmera corporal policial, para Android.
Tudo (cidade, NPCs, veículos, áudio, texturas) é **gerado por código** — o projeto não depende de assets externos e já abre jogável.

## 1. Requisitos
- Unity Hub + **Unity 2022.3 LTS** com os módulos **Android Build Support**, **Android SDK & NDK Tools** e **OpenJDK**.
- Celular Android 7.0+ (API 24), ARM64, com depuração USB ativada (para instalar via cabo) ou apenas permissão de instalar apps de fonte desconhecida.

## 2. Abrir e testar no computador
1. Unity Hub → **Add project from disk** → selecione a pasta `BodycamOpenWorld`.
2. Aguarde a importação (o pacote URP é baixado automaticamente).
3. O menu **Bodycam → 1 - Configurar Projeto** roda sozinho na 1ª abertura (cria o asset URP, configura Android e cria `Assets/Scenes/Main.unity`). Se não rodou, execute-o manualmente.
4. Abra `Assets/Scenes/Main.unity` e aperte **Play**.

Teclado/mouse no editor: **WASD** mover · **mouse** olhar (clique para capturar, Esc solta) · **Shift** correr · **Espaço** pular/freio de mão · **C** agachar → deitar → levantar · **E** usar/abrir/pegar/falar · **F** entrar/sair de veículo · **T** lanterna · **G** trocar qualidade.

## 3. Gerar o APK
1. **File → Build Settings → Android → Switch Platform** (uma vez).
2. Menu **Bodycam → 2 - Gerar APK**.
3. O arquivo sai em `Builds/BodycamOpenWorld.apk` (a 1ª compilação IL2CPP demora vários minutos).

## 4. Instalar no Android
- **Cabo (adb):** `adb install -r Builds/BodycamOpenWorld.apk`
- **Sem cabo:** copie o APK para o celular (Drive, USB, e-mail), abra-o e permita "instalar apps desconhecidos".

## 5. Controles no celular
Joystick virtual: toque e arraste na metade esquerda · Câmera: arraste na metade direita ·
Botões: **USAR** (abrir porta, pegar/soltar objeto, falar com NPC, sirene da viatura) · **PULAR** (freio de mão no carro) · **CORRER** (liga/desliga) · **AGACH.** (agacha → deita → levanta) · **VEÍC.** (entra/sai do veículo mais próximo) · **LUZ** (lanterna) · **GFX** (Baixo/Médio/Alto/Ultra) · **FPS** (30/45/60).
No carro: joystick = acelerar/virar.

## 6. O que a v0.1 tem
- **Câmera bodycam:** FOV largo com distorção de barril, granulação, aberração cromática, vinheta, leve dessaturação, balanço de peito (caminhar/correr/veículo), respiração, falhas digitais aleatórias, HUD com REC piscando, bateria, data/hora do jogo e GPS.
- **Mundo:** cidade 5×5 quarteirões + anel de floresta + estrada rural: ruas com faixas, calçadas, casas, lojas, mercado, posto, delegacia, hospital, oficina, área industrial, parque, torres. Edifícios com interior e portas que abrem.
- **Ambiente:** ciclo dia/noite (12 min = 24 h), clima (limpo/nublado/neblina/chuva), asfalto molhado com mais reflexo, postes e janelas acendem à noite, áudio 3D posicional procedural.
- **Personagem:** andar, correr, agachar, deitar, pular, abrir portas, pegar/soltar objetos, braços/mãos em 1ª pessoa, lanterna.
- **IA:** pedestres em rotas, pessoas conversando, trabalhadores, rotinas por horário (somem à noite), reação ao jogador correndo, fuga com sirene/acidentes; tráfego com semáforos, obstáculos e reciclagem de carros.
- **Veículos dirigíveis:** carro, moto, bicicleta, caminhonete, caminhão, viatura (sirene).
- **Missões (4) + eventos aleatórios** (acidente, alarme no mercado). NPCs com **!** dão missões; é opcional.
- **Sistema:** 4 níveis gráficos, limite de FPS, carregamento por quarteirão (streaming), static batching, autosave a cada 30 s e ao pausar, 100% offline.

## 7. Limitações honestas da v0.1
- Visual **não é ultrarrealista ainda**: geometria de caixas + texturas procedurais. Para chegar perto de BODYCAM, o caminho é substituir por assets PBR/scan (Quixel/Megascans, Synty POLYGON, etc.), baking de luz, probes de reflexo e personagens animados reais (Mixamo).
- "Compressão de vídeo" é simulada (grão + aberração + falhas). Macroblocos reais exigiriam um Renderer Feature com shader próprio.
- Reflexos usam o skybox + brilho PBR (sem SSR no URP mobile).
- Veículos usam física arcade; NPCs usam animação procedural e andam em rotas fixas (sem NavMesh).
- Não foi possível compilar/testar no Unity neste ambiente: espere pequenos ajustes de compilação na 1ª abertura.

## 8. Problemas comuns
- **Tudo rosa:** crie um URP Asset (Create → Rendering → URP Asset with Universal Renderer) e atribua em Project Settings → Graphics e Quality.
- **Erro de Input:** Project Settings → Player → Active Input Handling → *Input Manager (Old)* ou *Both*.
- **Lento no celular:** use GFX BAIXO e 30 FPS; o jogo escolhe o nível inicial pela RAM do aparelho.
- **Erro de shader/rosa só no APK:** rode "Bodycam → 1 - Configurar Projeto" de novo (inclui shaders no build).

## 9. Estrutura
```
Assets/Scripts/Core      Mats (texturas/materiais/malhas), Fx (áudio, partículas, Quality)
Assets/Scripts/Player    PlayerController, BodycamCamera, TouchControls, Hud
Assets/Scripts/World     CityGenerator, BlockStreamer, DayNightWeather, Door/Pickable
Assets/Scripts/AI        NpcAgent, NpcFactory
Assets/Scripts/Vehicles  SimpleVehicle, VehicleBuilder, TrafficCar, TrafficManager
Assets/Scripts/Systems   Missões, eventos aleatórios, salvamento, GameBootstrap
Assets/Editor            BodycamSetup (configura projeto e gera APK)
```

## 10. Próximos passos sugeridos
1. Trocar primitivas por modelos/PBR reais e baking de iluminação. 2. NavMesh + animações. 3. Mapa maior (estender `Map` em `CityGenerator`). 4. Interiores multi-andar. 5. Renderer Feature de compressão de vídeo. 6. Mais missões e armas/equipamentos.
