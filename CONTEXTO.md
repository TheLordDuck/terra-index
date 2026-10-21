# Contexto del proyecto: mod de Terraria "Recipe Index"

Resumen de la conversación con Claude Code (21/10/2026) para retomar el trabajo más adelante.

- **Autor:** thelordduck22
- **Plataforma:** tModLoader 1.4.4 (C#)
- **Carpeta del mod:** `RecipeIndex/` (dentro de esta carpeta)
- **Estado:** todo el código está escrito, pero **nunca se ha compilado ni probado**. En este PC no estaban
  instalados ni tModLoader ni .NET. El primer paso pendiente es compilarlo y corregir los errores que salgan.

---

## Lo que se pidió, en orden

1. **Un mod tipo NEI de Minecraft** para ver todas las recetas de todos los objetos.
   Se avisó de que ya existe el mod "Recipe Browser" en el Workshop, pero se decidió hacer uno propio.
2. **Autor:** cambiar `TuNombre` por `thelordduck22` en `build.txt`.
3. **Terminal de almacenamiento:**
   - Objeto nuevo, la Terminal, que se coloca **sobre una pared de fondo**.
   - Los cofres se conectan con **cable rojo** a la terminal.
   - Con clic derecho se abre un buscador que filtra **mientras escribes** los objetos de todos los cofres conectados.
   - Al hacer clic en un objeto, **se ilumina el cofre** donde está.
4. **Cambio:** en vez de fabricarla (originalmente 5 de piedra), **la vende la Mecánica por 5 de plata**.
5. **Aclaración:** la terminal tiene que estar en el **mismo cable** que conecta los cofres.
   Ya funcionaba así: el recorrido del cable empieza en la terminal.
6. **Soporte multijugador.**
7. **Sprites:** se revisaron los provisionales generados por script y al usuario le gustaron, así que se mantienen.
8. **Flecha** que apunta a los cofres iluminados fuera de pantalla, e **icono del mod**.

---

## Funcionalidades

### Índice de recetas (tipo NEI)
- Panel con una cuadrícula de todos los objetos (vanilla y de otros mods), con buscador.
  `@nombremod` filtra por mod, por ejemplo `@terraria` o `@calamity`. Clic derecho en el buscador lo borra.
- **Clic izquierdo** en un objeto: sus recetas. **Clic derecho:** recetas en las que se usa.
- Cada receta muestra:
  - El resultado.
  - Los ingredientes, en verde si tienes suficientes en el inventario y en rojo si no.
  - Los grupos tipo "Cualquier madera", con el icono rotando entre sus miembros.
  - Las estaciones de trabajo con su icono.
  - Las condiciones (cerca de agua, etc.).
- Navegación: se puede hacer clic en ingredientes y estaciones, hay un botón **Atrás** con historial, y la rueda
  del ratón sobre el panel derecho cambia de receta.
- **Teclas** (configurables en Controles):
  - `N`: abrir o cerrar.
  - `[`: recetas del objeto bajo el ratón.
  - `]`: usos del objeto bajo el ratón.

### Terminal de almacenamiento
- La vende la **Mecánica** por **5 de plata** (no tiene receta).
- Bloque de **2x2** que solo se coloca sobre una **pared de fondo**, como un cuadro (`AnchorWall`).
  Si se quita la pared, se cae.
- Se recorre el **cable rojo** empezando por las 4 casillas de la terminal, y se recogen los cofres por los que
  pasa (límite de 50 000 casillas de cable).
- **Clic derecho** en la terminal abre el panel con el buscador ya activo.
  - Muestra los objetos de todos los cofres conectados, con las cantidades sumadas.
  - Se actualiza cada medio segundo.
- **Clic** en un objeto: se cierra el panel, los cofres que lo tienen brillan en dorado durante 10 s (con luz y
  partículas) y sale un mensaje en el chat diciendo en cuántos cofres está.
- **Flecha dorada** en el borde de la pantalla hacia cada cofre iluminado que esté fuera de la vista, con la
  distancia en bloques.
- **Clic derecho** en un objeto: abre su receta en el índice.
- Se cierra con Esc (si no estás escribiendo), con la X o al alejarte más de 10 bloques.

### Multijugador
- El cliente pide el contenido al **servidor**, que es el único que conoce todos los cofres y los cables.
- El servidor escanea la red y responde con los totales y las posiciones de los cofres por objeto. Si la respuesta
  es grande, la divide en varios paquetes de unos 30 KB.
- Protecciones en el servidor:
  - Comprueba que en esa posición hay una terminal real.
  - Exige que el jugador esté a 20 bloques o menos.
  - Limita a una petición cada 10 ticks por jugador.
- En un jugador se usa el mismo código, pero calculado en local.
- El mod tiene que estar en el servidor y en los clientes.

---

## Estructura de archivos (`RecipeIndex/`)

| Archivo | Qué hace |
|---|---|
| `build.txt`, `description.txt`, `RecipeIndex.csproj` | Metadatos y proyecto del mod |
| `RecipeIndex.cs` | Clase del mod; recibe los paquetes de red (`HandlePacket`) |
| `RecipeIndexSystem.cs` | Teclas, capa de UI del índice y objeto bajo el ratón (`RecipeIndexPlayer` procesa las teclas) |
| `Common/RecipeDatabase.cs` | Índice de recetas por resultado y por ingrediente, estación → objeto y nombres de estaciones |
| `UI/UIHelpers.cs` | Traducciones, medir y recortar texto, dibujar ranuras de objeto, contar inventario |
| `UI/SearchBox.cs` | Caja de texto; solo una tiene el foco a la vez (`AnyFocused` bloquea las teclas del mod) |
| `UI/ItemGrid.cs` | Cuadrícula que solo dibuja lo visible; la usan el índice y la terminal |
| `UI/RecipeView.cs` | Panel de recetas: páginas, pestañas Recetas/Usos, historial, zonas clicables |
| `UI/BrowserState.cs` | Panel del índice de recetas |
| `UI/TerminalState.cs` | Panel de la terminal |
| `Content/Items/TerminalItem.cs` | Objeto Terminal y venta en la tienda de la Mecánica (`MechanicShop`) |
| `Content/Tiles/TerminalTile.cs` | Bloque de la terminal (2x2 en pared, clic derecho abre el panel) |
| `Content/Storage/StorageNetwork.cs` | Recorrido del cable rojo y escaneo de cofres (`StorageSnapshot`) |
| `Content/Storage/TerminalNet.cs` | Paquetes cliente ↔ servidor |
| `Content/Storage/TerminalSystem.cs` | Abrir y cerrar, datos recibidos, brillo de cofres, flechas fuera de pantalla |
| `Localization/es-ES_...hjson`, `en-US_...hjson` | Textos en español e inglés |
| `README.md` | Instrucciones de instalación y uso |

### Sprites (generados por script, en pixel art)

| Archivo | Tamaño | Descripción |
|---|---|---|
| `Content/Items/TerminalItem.png` | 24x24 | Terminal en el inventario |
| `Content/Tiles/TerminalTile.png` | 34x34 | 4 trozos de 16x16 con 2 px de separación (formato de Terraria para bloques 2x2) |
| `Content/Storage/ChestArrow.png` | 16x16 | Flecha apuntando a la derecha; se rota en el juego |
| `icon.png` | 80x80 | Icono del mod (lista de mods y Workshop) |
| `icon_small.png` | 30x30 | Icono pequeño de la lista de mods |

Estilo: caja gris con contorno oscuro, pantalla azul oscuro con líneas cian y un piloto verde; los iconos llevan
fondo azul con degradado y esquinas redondeadas. Para cambiarlos, mantén los mismos tamaños y nombres.

---

## Cómo compilar y probar

1. Instalar **tModLoader** desde Steam y abrirlo una vez.
2. Copiar o enlazar `RecipeIndex/` en `Documentos\My Games\Terraria\tModLoader\ModSources\`.
3. En tModLoader: **Workshop → Develop Mods → RecipeIndex → Build + Reload**.
4. Para multijugador: Host & Play con el mod activo, y que otro jugador use la terminal sobre cofres que nunca ha abierto.

---

## Pendiente / ideas

- **Compilar por primera vez** y corregir errores (nunca se ha compilado).
- La barra de desplazamiento de las cuadrículas no se puede arrastrar; solo funciona con la rueda.
- Al hacer clic en una estación de trabajo se ven las recetas de ese objeto, no todas las que se fabrican en esa estación.
- Los objetos de la terminal se agrupan por tipo; los modificadores (prefijos) no se distinguen.
- Los aparadores (dressers) como almacenamiento pueden detectarse solo en parte.
- Posibles mejoras: sacar objetos directamente desde la terminal, admitir otros colores de cable y
  ordenar por cantidad o por mod.
