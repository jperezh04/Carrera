# MOST CARBON NIGHTS '05 — Street Racing 2000s (Unity 6 URP)

Prototipo jugable de carreras callejeras arcade inspirado en **Need for Speed: Most Wanted (2005)** y **Need for Speed: Carbon (2006)**.

- **Carpeta del proyecto Unity**: [`Carreras/`](Carreras/)
- **Documentación detallada del juego y edición**: [`Carreras/Assets/Game/Documentation/README_STREET_RACING.md`](Carreras/Assets/Game/Documentation/README_STREET_RACING.md)

## Ejecución Rápida

1. Abre la carpeta **`Carreras`** en **Unity 6 (`6000.0.57f1`)** (Universal Render Pipeline).
2. Escenas listas para ejecutar en `Carreras/Assets/Game/Scenes/`:
   - **`MainMenu.unity`**: Menú inicial con vista previa 3D del vehículo sobre plataforma giratoria, selector de color de carrocería (Rojo, Azul, Negro, Blanco, Amarillo, Verde, Naranja, Plata) e inicio de carrera.
   - **`RaceTrack.unity`**: Circuito urbano nocturno editable con iluminación cálida/fría, vehículo controlable, **2 vehículos rivales que navegan por los 24 Waypoints del circuito (`FollowWP`)**, cámara en tercera persona, cuenta regresiva `3, 2, 1, GO!`, sistema de vueltas y checkpoints con validación de orden, **Minimapa 2D de Waypoints en el HUD**, pausa y pantalla de finalización.
3. Pulsa **Play (▶)** en cualquiera de las dos escenas.

## Controles

| Tecla / Control | Acción |
| :--- | :--- |
| **W / Flecha Arriba (↑)** | Acelerar |
| **S / Flecha Abajo (↓)** | Frenar / Marcha atrás |
| **A / D o Flechas Izquierda / Derecha (← / →)** | Girar |
| **Espacio** | Freno de mano (Derrape arcade) |
| **R** | Recuperar el vehículo en posición segura del circuito |
| **Escape** | Pausar / Reanudar carrera |

## Sistema de Waypoints (Guía Sesión 6) y Carros Rivales (`FollowWP`)

- **24 Waypoints en el circuito (`Waypoints_Circuit/Waypoint_00`..`Waypoint_23`)**: Visibles tanto sobre la pista 3D (con bases luminosas y balizas flotantes cian) como en la Scene View (Gizmos de esferas y líneas) y en el **Minimapa 2D del HUD (`TopRight_WaypointMapPanel`)**.
- **2 Carros Rivales (`RivalCar_01_FollowWP` y `RivalCar_02_FollowWP`)**: Recorren los 24 waypoints utilizando la lógica de [`FollowWP.cs`](Carreras/Assets/Game/Scripts/Race/FollowWP.cs) (`Vector3.Distance`, `Quaternion.LookRotation`, `Quaternion.Slerp`, `transform.Translate(0, 0, speed * Time.deltaTime)`).
