# Recipe Index

Explorador de recetas para Terraria (tModLoader 1.4.4), al estilo NEI de Minecraft.

## Instalación (desarrollo)

1. Instala **tModLoader** desde Steam y ábrelo una vez.
2. Copia (o enlaza) esta carpeta `RecipeIndex` dentro de
   `Documentos\My Games\Terraria\tModLoader\ModSources\`.
   Para un enlace en vez de copiar (PowerShell como administrador):
   `New-Item -ItemType Junction -Path "$HOME\Documents\My Games\Terraria\tModLoader\ModSources\RecipeIndex" -Target "<ruta a esta carpeta>"`
3. En tModLoader: **Workshop → Develop Mods → RecipeIndex → Build + Reload**.

## Uso

| Tecla (configurable en Controles) | Acción |
|---|---|
| `N` | Abrir/cerrar el índice |
| `[` con el ratón sobre un objeto | Ver sus recetas |
| `]` con el ratón sobre un objeto | Ver en qué se usa |

- Clic izquierdo en un objeto: recetas. Clic derecho: usos.
- Dentro de una receta puedes hacer clic en ingredientes y estaciones para navegar; **Atrás** vuelve.
- Rueda del ratón sobre el panel derecho: cambiar de receta.
- Buscador: escribe un nombre, o `@calamity` / `@terraria` para filtrar por mod. Clic derecho lo borra.
- Los ingredientes salen en verde si tienes suficientes en el inventario y en rojo si no.

## Terminal de almacenamiento

1. Compra la **Terminal** a la **Mecánica** (5 de plata).
2. Colócala sobre una **pared de fondo** (como un cuadro, ocupa 2x2).
3. Pasa **cable rojo** por encima de la terminal y de cada cofre que quieras conectar. Todo tiene que estar en el
   mismo cable: la red se recorre desde la terminal, así que si el cable no la toca no verá ningún cofre.
4. Haz **clic derecho** en la terminal y escribe: la lista se filtra mientras escribes.
5. **Clic** en un objeto: los cofres que lo tienen brillan durante 10 segundos.
   **Clic derecho**: abre la receta de ese objeto en el índice.

Se cierra con Esc, con la X o al alejarte.

**Multijugador:** el mod tiene que estar instalado en el servidor y en todos los clientes (tModLoader lo
sincroniza solo al entrar). El escaneo lo hace el servidor, que es quien conoce todos los cofres, y mientras el
panel está abierto se actualiza cada medio segundo, así que ves los cambios que hagan otros jugadores.
