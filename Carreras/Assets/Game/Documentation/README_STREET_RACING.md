# MOST CARBON NIGHTS '05 — Street Racing 2000s (Unity 6 URP)

Prototipo jugable de carreras callejeras arcade inspirado en la atmósfera urbana nocturna de **Need for Speed: Most Wanted (2005)** y **Need for Speed: Carbon (2006)**.

---

## 1. Cómo Abrir y Ejecutar el Proyecto

1. Abre el proyecto en **Unity 6 (`6000.0.57f1`)** (Universal Render Pipeline).
2. Las escenas principales se encuentran en `Assets/Game/Scenes/`:
   - **`Assets/Game/Scenes/MainMenu.unity`**: Menú inicial con vista previa 3D interactiva del coche sobre plataforma giratoria, selector de color de carrocería y botón de inicio de carrera.
   - **`Assets/Game/Scenes/RaceTrack.unity`**: Escena principal de carrera con el circuito urbano nocturno completo, vehículo controlable, iluminación, checkpoints, HUD, pausa y pantalla de finalización.
3. Ambas escenas están registradas en **File > Build Settings**.
4. Para jugar:
   - Abre `MainMenu.unity` (para elegir el color de la carrocería e iniciar la carrera) o directamente `RaceTrack.unity` y pulsa el botón **Play (▶)** en la parte superior del Editor.
5. Si en algún momento deseas reconstruir desde cero todos los materiales, prefabs y escenas automáticamente, usa el menú superior del Editor:
   - **`Tools > Street Racing 2000s > 1. Build & Wire Complete Game (Scenes + Prefabs)`**
   - **`Tools > Street Racing 2000s > 2. Run Automated Verification Suite`**

---

## 2. Controles del Vehículo

El sistema utiliza el paquete **Unity Input System** y soporta teclado (y mando opcional):

| Tecla / Control | Acción |
| :--- | :--- |
| **W** o **Flecha Arriba (↑)** | Acelerar progresivamente |
| **S** o **Flecha Abajo (↓)** | Frenar y, al detenerse, marcha atrás |
| **A / D** o **Flechas Izquierda / Derecha (← / →)** | Dirección progresiva adaptada a la velocidad |
| **Espacio (Space)** | Freno de mano para iniciar derrapes arcade |
| **R** | Recuperar el vehículo en el último checkpoint / posición segura del circuito |
| **Escape (Esc)** | Pausar o reanudar la carrera |

### Ajustes del comportamiento del coche desde el Inspector

Selecciona `PlayerCar_StreetRacer` en la escena `RaceTrack.unity` (o el prefab `Assets/Game/Prefabs/Vehicle/StreetRacer_Coupe.prefab`) y ajusta en el componente **`ArcadeCarController`**:
- `Max Speed Kmh`: Velocidad punta en km/h (por defecto `230 km/h`).
- `Acceleration`: Aceleración longitudinal y curva de potencia (`Acceleration Curve`).
- `Brake Deceleration`: Fuerza de frenado.
- `Max Steer Angle Low Speed` / `High Speed`: Ángulo de giro a baja y alta velocidad.
- `Grip` y `Lateral Stiffness`: Agarre lateral en curva.
- `Handbrake Grip Multiplier`, `Handbrake Yaw Boost` y `Drift Momentum Keep`: Comportamiento del derrape arcade y recuperación del agarre (`Grip Recovery Time`).

---

## 3. Cómo Cambiar el Color del Coche

1. **Desde el juego (`MainMenu.unity`)**:
   - En el panel izquierdo del menú principal, haz clic en cualquiera de los botones de color (**Rojo, Azul, Negro, Blanco, Amarillo, Verde, Naranja, Plata**) o usa **`< COLOR ANTERIOR`** / **`SIGUIENTE COLOR >`**.
   - El color se aplica en tiempo real sobre la carrocería del coche en la plataforma 3D y se guarda automáticamente (`GameSettings.PaintColorIndex`) para aplicarse al comenzar la carrera.
2. **Desde el Inspector (`CarPaint`)**:
   - El componente `CarPaint` del vehículo contiene la lista explícita `Slots` con los `Renderer` e índices de material que corresponden exclusivamente a la pintura de carrocería (`Mat_CarBodyPaint`).
   - De este modo, **los neumáticos, llantas, cristales, faros, pilotos traseros y fibra de carbono conservan siempre su apariencia original** y no se modifican los assets originales en disco.

---

## 4. Cómo Editar el Circuito y el Ancho de la Carretera Visualmente

El circuito no es un bloque monolítico: está gestionado por el objeto **`RaceTrack_EditableSpline`** en `RaceTrack.unity` mediante los componentes `TrackPath` y `TrackEnvironmentDecorator`.

### Editar el recorrido y la altura en la Scene View
1. En la jerarquía de `RaceTrack.unity`, selecciona **`RaceTrack_EditableSpline`**.
2. En la **Scene View**, verás los nodos numerados (`#0 [SALIDA / META]`, `#1`, `#2`, ...) junto con las líneas del centro y los bordes de la calzada.
3. Usa los tiradores 3D (**Position Handles**) directamente en la Scene View para **mover cualquier punto de control en X, Z o cambiar su altura (Y)** para crear subidas, pasos elevados o bajadas.
4. En el Inspector de `TrackPath` puedes:
   - Activar o desactivar **"Regenerar carretera automáticamente al mover nodos"**.
   - Pulsar **"+ Insertar Punto"** para añadir un nuevo nodo tras el punto seleccionado.
   - Pulsar **"- Eliminar Punto"** para borrar el nodo seleccionado.
   - Pulsar **"Nivelar Altura (Y=0)"** para aplanar todo el recorrido.

### Modificar el ancho de la carretera y barreras
En el Inspector de `TrackPath` (`RaceTrack_EditableSpline`):
- **`Road Width`**: Cambia el ancho de la calzada asfaltada (de `8 m` a `28 m`, por defecto `14 m`).
- **`Sidewalk Width`** y **`Curb Height`**: Ajustan el ancho y altura de las aceras/bordillos.
- **`Barrier Height`**: Ajusta la altura de los muros de contención.

#### Regenerar el trazado y los elementos asociados
Tras realizar cambios manuales en el Inspector, pulsa el botón verde del Inspector de `TrackPath`:
- **"Regenerar Carretera, Barreras y Checkpoints"**: Reconstruye sin duplicar geometría la malla de asfalto (`RoadSurface`), las líneas viales (`RoadMarkings`), las aceras (`SidewalksAndCurbs`), los muros físicos (`TrackBarriers`), el punto de aparición (`SpawnPoint`), los checkpoints ordenados (`Checkpoints`) y los **24 Waypoints del circuito (`Waypoints_Circuit/Waypoint_00`..`Waypoint_23`)**.
- **"Actualizar Farolas y Meta"**: Reubica automáticamente el pórtico de Salida/Meta, las farolas nocturnas y las barreras de curva a lo largo del nuevo trazado.
- **"Reubicar Edificios Exteriores"**: Regenera los bloques de edificios urbanos respetando el nuevo ancho del corredor vial.

---

## 5. Sistema de Waypoints (Guía Sesión 6) y 2 Vehículos Rivales (`FollowWP`)

Siguiendo la guía de laboratorio **Sesión 6: Waypoints**, el circuito cuenta con **24 Waypoints (`Waypoint_00` a `Waypoint_23`)** y **2 carros rivales adicionales** que recorren el trazado utilizando el script [`FollowWP.cs`](../Scripts/Race/FollowWP.cs):

### Lógica `FollowWP` implementada
- **`waypoints` (`GameObject[]`) y `currentWP` (`int`)**: Arreglo ordenado con los 24 nodos del circuito (`Waypoints_Circuit`).
- **Avance de nodo por distancia**: Cuando `Vector3.Distance(flatSelf, targetPos) < accuracy`, incrementa `currentWP++` y reinicia a `0` al completar la vuelta (`if (currentWP >= waypoints.Length) currentWP = 0;`).
- **Giro progresivo y traslación**:
  - `Quaternion lookatWP = Quaternion.LookRotation(dir);`
  - `this.transform.rotation = Quaternion.Slerp(this.transform.rotation, lookatWP, rotSpeed * Time.deltaTime);`
  - `this.transform.Translate(0, 0, speed * Time.deltaTime);`
- **Dos vehículos rivales en `RaceTrack.unity`**:
  1. **`RivalCar_01_FollowWP`** (Coupé naranja metálico): `speed = 33`, `rotSpeed = 6.2`, `laneOffset = -2.8m` (carril izquierdo).
  2. **`RivalCar_02_FollowWP`** (Skyline amarillo/dorado): `speed = 29`, `rotSpeed = 5.5`, `laneOffset = +2.8m` (carril derecho).

### Visualización de los Waypoints en 3D y en el Mapa 2D (HUD)
1. **En la Pista 3D y en la Scene View**:
   - Cada `Waypoint_00`..`Waypoint_23` dentro de `RaceTrack_EditableSpline/Waypoints_Circuit` incluye una base circular luminosa sobre el asfalto (`WP_XX_RoadPad`) y una baliza flotante cian (`WP_XX_Beacon`), además de esferas y líneas de conexión Gizmo en la Scene View (`WaypointCircuitManager.cs`).
2. **En el Mapa 2D del HUD (`TopRight_WaypointMapPanel` / `WaypointMinimapHUD.cs`)**:
   - Panel superior derecho en pantalla durante la carrera que dibuja el trazado completo del circuito, los **24 Waypoints numerados (`WP0`..`WP23`)**, la posición del jugador (**`TÚ`** en verde) y los 2 carros rivales (**`R1`** en naranja y **`R2`** en amarillo), resaltando en tiempo real hacia qué `Waypoint` se dirige cada carro (`R1→WP#... | R2→WP#...`).

---

## 6. Cómo Añadir, Mover o Quitar Objetos del Escenario

Todos los elementos del entorno están organizados en contenedores claros dentro de `RaceTrack_EditableSpline`:
- **`GeneratedRoad`**: Superficie de asfalto, marcas viales, aceras y muros físicos con sus `MeshRenderer` y `MeshCollider` accesibles en el Inspector.
- **`Waypoints_Circuit`**: Los 24 waypoints (`Waypoint_00`..`Waypoint_23`) que siguen los carros `FollowWP` y se muestran en el minimapa. Puedes mover cualquier waypoint individualmente en la Scene View o ajustar `Waypoint Count` en `TrackPath`.
- **`StartFinish_Area`**: Pórtico de salida/meta, línea ajedrezada, pancartas e iluminación de parrilla.
- **`StreetLights_Group`**: Farolas individuales (`StreetLight_00`, `StreetLight_01`, ...), cada una con su malla, bombilla emisiva y `PointLight`.
- **`CornerBarriers_Group`**: Barreras rojas/blancas de aviso en curvas y vallas publicitarias (`Billboard_00`, ...).
- **`CityBuildings_Group`**: Edificios urbanos (`Building_00`, `Building_01`, ...) instanciados a partir de los prefabs en `Assets/Game/Prefabs/Environment/`.

Puedes seleccionar, mover (`W`), rotar (`E`), escalar (`R`), duplicar (`Ctrl+D`) o eliminar (`Supr`) cualquier edificio, farola, valla o prop directamente desde la Scene View sin tocar código.

---

## 7. Cómo Cambiar el Número de Vueltas

1. En `RaceTrack.unity`, selecciona el objeto **`RaceManager`** en la jerarquía.
2. En el Inspector del componente `RaceManager`, cambia el campo **`Total Laps`** (por defecto `3`).
3. También puedes configurar **`Countdown Steps`** (`3, 2, 1, GO!`) y **`Countdown Step Duration`**.

---

## 8. Cómo Cambiar la Iluminación y el Post-Procesado

- **Iluminación global y niebla**: En `RaceTrack.unity`, selecciona **`DirectionalLight_MoonAndCityGlow`** para ajustar la luz direccional nocturna, o abre **Window > Rendering > Lighting > Environment** para modificar el color ambiental y la niebla exponencial.
- **Farolas del circuito**: En `RaceTrack_EditableSpline` (`TrackEnvironmentDecorator`), puedes modificar `Warm Street Color` (ámbar cálido), `Cool Accent Color` (cian frío), `Street Light Spacing` y `Real Light Every N`.
- **Post-procesado (URP Volume)**: Selecciona **`GlobalVolume_NightPostProcess`** (perfil `Assets/Game/Settings/StreetRacing_NightVolumeProfile.asset`) para ajustar **Bloom**, **ACES Tonemapping**, **Color Adjustments**, **Vignette** y **Motion Blur**.

---

## 9. Cómo Sustituir el Coche

El proyecto incluye dos prefabs de vehículos listos para URP en `Assets/Game/Prefabs/Vehicle/`:
1. **`StreetRacer_Coupe.prefab`** (por defecto): Coupé deportivo de alta fidelidad basado en `LabCoupe_MD.fbx` con materiales URP de carrocería, fibra de carbono, cristales, llantas, faros y pilotos traseros emisivos.
2. **`StreetRacer_Skyline.prefab`**: Alternativa basada en `Model_Skyline by BUMSTRUM(3DMaesen) (Prototype).fbx` configurada con el mismo `ArcadeCarController`.

Para sustituir el coche por otro modelo o por `StreetRacer_Skyline.prefab`:
1. Arrastra el prefab deseado a `RaceTrack.unity` en la posición de `SpawnPoint`.
2. Asigna el nuevo coche en el campo **`Player Car`** de `RaceManager` y en el campo **`Target`** de `Main Camera` (`RaceCamera`).

---

## 10. Inventario de Recursos Reutilizados y Creados

### Assets preexistentes reutilizados
- **Modelo de vehículo principal (`LabCoupe_MD.fbx`)** y texturas de fibra de carbono/hormigón: procedentes del paquete de muestra de Unity `Assets/HDRPLab/` (convertidos mediante materiales propios en `Assets/Game/Materials/` al pipeline **URP** sin alterar los archivos originales).
- **Modelo de vehículo secundario (`Model_Skyline by BUMSTRUM(3DMaesen) (Prototype).fbx`)**, audios de motor (`Engine_Generic_Idle/Low/Med/High.wav`), derrape (`Skid_Asphalt.wav`), viento (`Vehicle_Wind.wav`), colisiones (`Impact2..4.wav`) y texturas de humo/marcas de neumático: procedentes de `Assets/Realistic Car Controller Pro/`.
- **Edificios urbanos, farolas, pórticos, barreras y carteles**: procedentes de los paquetes **Kenney** incluidos en el proyecto (`kenney_city-kit-commercial_2.1`, `kenney_city-kit-roads`, `kenney_racing-kit`, licencia **CC0 1.0 Public Domain**).
- **Tipografías**: `Roboto-Bold.ttf` (Apache 2.0) y `good times rg.ttf` incluidas en el proyecto.

### Recursos creados por el proyecto (`Assets/Game/`)
- **Scripts modulares (`Assets/Game/Scripts/`)**:
  - `Core/`: `GameSettings.cs`, `SceneLoader.cs`
  - `Vehicle/`: `CarInput.cs`, `ArcadeCarController.cs`, `CarPaint.cs`, `CarLights.cs`, `CarAudio.cs`, `CarEffects.cs`, `CarBodyTilt.cs`, `SkidmarkManager.cs`
  - `Camera/`: `RaceCamera.cs`
  - `Race/`: `RaceCheckpoint.cs`, `RaceManager.cs`, `FollowWP.cs`, `WaypointCircuitManager.cs`
  - `Environment/`: `TrackPath.cs`, `TrackEnvironmentDecorator.cs`
  - `UI/`: `MainMenuController.cs`, `RaceUIController.cs`, `WaypointMinimapHUD.cs`
- **Herramientas de Editor (`Assets/Game/Editor/`)**:
  - `TrackPathEditor.cs` (editor visual de nodos y ancho de pista en Scene View)
  - `StreetRacingProjectBuilder.cs` (constructor automatizado de escenas, materiales y prefabs)
  - `StreetRacingVerificationSuite.cs` (suite de validación estática y lógica de carrera)
- **Escenas, Prefabs, Materiales URP y Perfiles (`Assets/Game/Scenes`, `Prefabs`, `Materials`, `Settings`)**.

---

## 11. Limitaciones Conocidas

- Los 2 vehículos rivales utilizan el navegador cinemático por waypoints `FollowWP` de la guía de laboratorio (Sesión 6: Waypoints) con carriles desplazados (`laneOffset`) y ajuste de altura por raycast sobre el asfalto.
- La iluminación puntual en tiempo real se activa de forma alternada en las farolas (`realLightEveryN = 2`) combinada con esferas emisivas y Bloom en todas las farolas para mantener un alto rendimiento estable en URP Forward.

