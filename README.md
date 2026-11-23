# Rust Survival — Unity

Proyecto de supervivencia y construcción multijugador para Windows y Android (iOS queda preparado a nivel de arquitectura, pero requiere equipo y firma Apple para compilar/probar).

## Versión base

- Unity **2022.3.62f1 LTS**.
- Universal Render Pipeline (URP) 14.
- Nuevo Input System.
- Netcode for GameObjects 1.9.1 + Unity Transport 1.3.4.
- Unity Authentication 2.7.4, Lobby 1.2.1 y Relay 1.0.5.

Estas versiones se fijaron tomando como base las versiones publicadas por Unity para la línea 2022.3. No abras este primer prototipo con Unity 6 hasta preparar y probar una migración de los paquetes de Lobby/Relay independientes.

## Estado actual

El repositorio contiene la primera base jugable y un asistente que genera una escena de prototipo y prefabs desde Unity. **Todavía no contiene un ejecutable compilado ni una escena que haya sido abierta/compilada en Unity en esta sesión.** Los modelos creados por el asistente son greybox 3D con primitivas: sirven para probar escala, colisiones y bucles de juego, pero no son los modelos PBR finales.

Implementado en código:
- Movimiento en tercera persona con CharacterController, PC, gamepad y joystick móvil.
- Mirada con ratón, stick derecho y zona táctil.
- Generador de terreno de 500 x 500 m con máscaras de bioma de bosque, llanura y roca.
- Nodos de árbol, roca, arbustos de comida y cachés de agua que se pueden recolectar.
- Inventario replicado de madera, piedra, comida y agua.
- Salud, hambre y sed con consumo por teclado, gamepad o botones táctiles.
- Construcción básica con vista previa verde/roja, rotación y validación de servidor.
- Sincronización NGO de jugador, recursos cosechables y piezas colocadas.
- Flujo básico de Lobby + Relay para crear y unirse a salas con código.
- Ciclo día/noche y niebla de distancia ligera.
- HUD de supervivencia/inventario y controles táctiles generados.

## Estructura

```
Assets/_Game/
  Art/                 Materiales, modelos y texturas (arte final pendiente)
  Docs/                Reglas de autoridad y pipeline gráfico
  Editor/              Asistente de generación de escena y prefabs
  Prefabs/              Lo genera el asistente de Unity
  Scenes/               Lo genera el asistente de Unity
  Scripts/
    Building/          Colocación y validación de estructuras
    Core/              Ajustes de rendimiento
    Networking/        Relay, Lobby y sincronización NGO
    Player/            Movimiento y controles
    Resources/         Recolección e inventario
    Survival/          Salud, hambre y sed
    UI/                HUD y controles táctiles
    World/             Generación de terreno, recursos y ciclo día/noche
Packages/manifest.json
ProjectSettings/ProjectVersion.txt
```

## Preparar el proyecto en Windows

1. Instala **Unity Hub** y Unity Editor **2022.3.62f1**.
2. En Unity Hub instala también **Android Build Support**, incluyendo Android SDK & NDK Tools y OpenJDK. Instala Windows Build Support para crear builds de PC.
3. Clona el repositorio y abre la carpeta raíz con **Unity Hub > Add > Add project from disk**.
4. Espera a que Package Manager descargue todos los paquetes de `Packages/manifest.json`.
5. En **Edit > Project Settings > Player > Other Settings**, establece **Active Input Handling = Input System Package (New)**. Acepta el reinicio del Editor.
6. Si no existe un URP Asset activo, crea uno en **Assets > Create > Rendering > URP Asset (with Universal Renderer)**. Asígnalo en **Edit > Project Settings > Graphics > Default Render Pipeline** y en **Project Settings > Quality** para los niveles que se usarán.
7. En el menú superior ejecuta **Rust Survival > Generate Prototype Scene and Prefabs**. El asistente genera `Assets/_Game/Scenes/PrototypeWorld.unity`, los prefabs de prueba, HUD, controles y la entrada en Build Settings.
8. Abre `PrototypeWorld.unity` y guarda el proyecto. Comprueba en la consola que no haya errores de compilación.

## Configurar Lobby y Relay

1. Abre **Edit > Project Settings > Services** y vincula el proyecto a un Unity Cloud Project (Project ID).
2. En Unity Dashboard, activa Authentication, Lobby y Relay en el mismo proyecto/entorno.
3. Ejecuta la escena. El panel temporal de desarrollo aparece arriba a la izquierda.
4. Pulsa **CREAR SALA / HOST**. Se muestra el código Lobby; compártelo con otro cliente.
5. En la segunda instancia del juego introduce ese código y pulsa **UNIRSE A SALA**.

El primer prototipo utiliza un host jugador como autoridad a través de Relay. **No es un servidor dedicado ni ofrece protección antitrampas frente al propio anfitrión.** Para servidores públicos persistentes habrá que migrar la misma lógica de validación a procesos de servidor dedicado.

## Controles

| Acción | PC | Móvil |
|---|---|---|
| Moverse | WASD | Joystick izquierdo |
| Mirar | Ratón | Arrastrar mitad derecha |
| Saltar | Espacio | JUMP |
| Correr | Shift | RUN (mantener) |
| Agacharse | Ctrl | CROUCH (mantener) |
| Recolectar | Clic izquierdo o E | HIT |
| Comer | H | EAT |
| Beber | J | DRINK |
| Activar construcción | V | BUILD |
| Colocar pieza | Enter o clic izquierdo | PLACE |
| Girar pieza | R | TURN |
| Elegir pieza | 1 / 2 / 3 | (selección táctil pendiente) |
| Cancelar construcción | Escape | BUILD de nuevo |

## Límites conocidos de esta primera base

- La escena y los prefabs se generan en el Editor; no se han compilado ni probado en un dispositivo desde esta sesión.
- Modelos PBR finales, normales, AO, metallic/smoothness, texturas ASTC, animaciones, audio, LOD Groups completos y vegetación final aún no están incorporados.
- El relieve/biomas son procedurales; faltan reglas de distribución por bioma, costa/agua navegable, puntos de interés y mapas de mundo.
- El movimiento en red usa RPC de input y simulación de servidor sin predicción/reconciliación; la sensación debe ajustarse con pruebas de latencia.
- El HUD usa sliders y texto simples; falta UI de inventario, hotbar, crafting, menú de pausa, ajustes y respawn.
- El mundo no tiene persistencia tras apagar la sesión, protección de zonas, decay ni sistema de permisos de construcción.
- El objetivo de 60 FPS debe medirse en móviles concretos. GI en tiempo real y niebla volumétrica no están activadas: la versión inicial usa iluminación y niebla ligera para priorizar rendimiento.

## Calidad visual y assets

Los assets finales serán modelos 3D originales o de licencia verificada, con Albedo, Normal, AO y mapas Metallic/Smoothness cuando correspondan. Se prepararán LOD0/LOD1/LOD2, colisiones simplificadas, GPU instancing y overrides ASTC por plataforma. Consulta `Assets/_Game/Docs/ART_PIPELINE.md`.

No se han copiado modelos ni texturas comerciales de Rust. Mantén la licencia de cada recurso junto a su asset.

## Seguridad multijugador

Los clientes envían intención; el servidor controla inventario, desgaste de recursos, supervivencia y aparición de estructuras. Consulta `Assets/_Game/Docs/NETWORK_AUTHORITY.md`. La validación está en fase prototipo y requiere pruebas de abuso, rate limiting, persistencia y auditoría antes de publicar.
