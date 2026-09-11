# One Thunder

Un action-platformer 2D construido en **Godot 4.7 + C#**, con combate cuerpo a cuerpo, sistema de postura, parry, dash y una IA enemiga 

---

## Arquitectura técnica

El proyecto está organizado en **scripts parciales (`partial class`)** para separar responsabilidades sin fragmentar la identidad del nodo. Por ejemplo, `EnemyAI` se compone de:

- `EnemyAI.cs` — núcleo: ciclo de vida, máquina de estados, referencias a nodos
- `EnemyMovement.cs` — patrulla, persecución, gravedad
- `EnemyCombat.cs` — ataque, daño, stun, muerte
- `EnemyDetection.cs` — detección del jugador por distancia real, con histéresis
- `EnemyAnimator.cs` — animación y orientación del sprite

Este patrón se repite en `Player` (movimiento, combate, dash, parry, animación) y en los sistemas core (`HealthSystem`, `PostureSystem`, `HealthBar`), manteniendo cada archivo enfocado en una sola cosa sin perder cohesión del objeto completo.

### Sistemas destacados

- **Detección por distancia con histéresis** (`AggroRange` / `DeaggroRange` / `AttackRange`) en vez de `Area2D` de colisión — persecución más estable y sin parpadeos de estado en los bordes del rango.
- **Barra de vida pixel-art por frames discretos**, usando `AtlasTexture` sobre una tira de sprites en vez de `ProgressBar` genérico, para mantener la identidad visual del arte.
- **Sistema de postura independiente de la salud**, que permite romper la guardia del enemigo y dejarlo vulnerable a un golpe crítico.
- **Máquina de estados explícita** (`Patrol`, `Idle`, `Chase`, `PreAttack`, `Attack`, `Hurt`, `Stunned`, `Dead`) con telegrafía de ataque, para que el combate se sienta leíble y justo.

---

## Estructura del proyecto

```
Scenes/
├── Attacks/         # Proyectiles y hitboxes reutilizables
├── Enemy/           # Escena del enemigo
├── Level/           # Niveles jugables
├── Player/          # Escena del jugador
└── UI/              # HUD

Scripts/
├── Combat/          # Hitbox, Hurtbox, Projectile
├── Enemy/           # IA enemiga (partial classes)
├── Level/           # Lógica de nivel y transiciones
├── Player/          # Movimiento, combate, dash, parry
├── Systems/         # HealthSystem, PostureSystem, HealthBar, SceneTransition
└── UI/               # HUD
```

---

## Requisitos

- **Godot 4.7** (build **.NET / Mono** — el proyecto usa C#, no corre en la build Standard)
- **.NET SDK** instalado (necesario para que Godot compile los ensamblados del proyecto)

## Cómo correrlo

1. Abrí Godot 4.7 .NET
2. Importá el proyecto apuntando a `project.godot`
3. Dejá que el editor genere los ensamblados de C# la primera vez (puede tardar un poco)
4. Ejecutá la escena principal

---

## Stack

- **Motor:** Godot 4.7
- **Lenguaje:** C#
- **Renderer:** GL Compatibility
- **Control de versiones:** Git (con `godot-git-plugin`)
