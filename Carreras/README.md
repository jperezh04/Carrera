# MOST CARBON NIGHTS '05 — Street Racing 2000s (Unity 6 URP)

Prototipo jugable de carreras callejeras arcade inspirado en **Need for Speed: Most Wanted (2005)** y **Need for Speed: Carbon (2006)**.

> Documentación completa dentro del proyecto: [`Assets/Game/Documentation/README_STREET_RACING.md`](Assets/Game/Documentation/README_STREET_RACING.md)

## Ejecución Rápida

1. Abre el proyecto en **Unity 6 (`6000.0.57f1`)**.
2. Escenas listas para ejecutar en `Assets/Game/Scenes/`:
   - **`Assets/Game/Scenes/MainMenu.unity`**: Menú inicial con vista previa 3D del vehículo sobre plataforma giratoria, selector de color de carrocería (Rojo, Azul, Negro, Blanco, Amarillo, Verde, Naranja, Plata) e inicio de carrera.
   - **`Assets/Game/Scenes/RaceTrack.unity`**: Circuito urbano nocturno editable con iluminación cálida/fría, vehículo controlable, **2 vehículos rivales que navegan por los 24 Waypoints del circuito (`FollowWP`)**, cámara en tercera persona, cuenta regresiva `3, 2, 1, GO!`, sistema de vueltas y checkpoints con validación de orden, **Minimapa 2D de Waypoints en el HUD**, pausa y pantalla de finalización.
3. Pulsa **Play (▶)** en cualquiera de las dos escenas.

## Controles

- **W / Flecha Arriba (↑)**: Acelerar
- **S / Flecha Abajo (↓)**: Frenar / Marcha atrás
- **A / D o Flechas Izquierda / Derecha (← / →)**: Girar
- **Espacio**: Freno de mano (Derrape arcade)
- **R**: Recuperar el vehículo en posición segura del circuito
- **Escape**: Pausar / Reanudar carrera

## Sistema de Waypoints (Guía Sesión 6) y Carros Rivales (`FollowWP`)

- **24 Waypoints en el circuito (`Waypoints_Circuit/Waypoint_00`..`Waypoint_23`)**: Visibles tanto sobre la pista 3D (con bases luminosas y balizas flotantes cian) como en la Scene View (Gizmos de esferas y líneas) y en el **Minimapa 2D del HUD (`TopRight_WaypointMapPanel`)**.
- **2 Carros Rivales (`RivalCar_01_FollowWP` y `RivalCar_02_FollowWP`)**: Recorren los 24 waypoints utilizando la lógica de [`FollowWP.cs`](Assets/Game/Scripts/Race/FollowWP.cs) (`Vector3.Distance`, `Quaternion.LookRotation`, `Quaternion.Slerp`, `transform.Translate(0, 0, speed * Time.deltaTime)`).

## Edición Visual del Circuito

1. Abre `Assets/Game/Scenes/RaceTrack.unity` y selecciona el objeto **`RaceTrack_EditableSpline`** en la jerarquía.
2. Mueve los nodos numerados (`#0 [SALIDA / META]`, `#1`, `#2`...) directamente en la **Scene View** con los tiradores 3D (incluyendo su altura `Y`), o modifica `Road Width` y `Waypoint Count` en el Inspector.
3. Pulsa **"Regenerar Carretera, Barreras y Checkpoints"** en el Inspector de `TrackPath` (o deja activada la regeneración automática) para actualizar la calzada, aceras, muros físicos, checkpoints y los 24 waypoints sin duplicar geometría.

