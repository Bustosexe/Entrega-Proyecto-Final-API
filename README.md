# TechHub: gestión comercial de hardware y componentes de PC

El proyecto está separado en **backend** y **frontend**, unidos por una API REST:

```
TechHub.App  (app Android, .NET MAUI)  ──HTTP + JWT──►  TechHub.Api  (ASP.NET Core 10)  ──EF Core──►  SQL Server
   APK / emulador                                          MonsterASP.NET                            MonsterASP.NET
```

| Proyecto | Tecnología | Qué hace |
|---|---|---|
| `TechHub.Api` | ASP.NET Core 10, EF Core 10 (Code-First), SQL Server, JWT, Swagger | Lógica de negocio, seguridad, stock, pedidos e imágenes |
| `TechHub.App` | .NET MAUI 10 (Android), MVVM con CommunityToolkit.Mvvm | App móvil con tres perfiles: visitante, comprador y administrador |

## Perfiles de la app

| Perfil | Login | Qué puede hacer |
|---|---|---|
| **Visitante** | No | Ver el catálogo con fotos, buscar, filtrar por categoría y ordenar por precio. Ver el detalle de cada producto y armar el carrito. |
| **Comprador** | Sí (se registra desde la app) | Lo mismo que el visitante, más confirmar compras, ver *Mis pedidos*, cancelar un pedido pendiente y editar su perfil. |
| **Administrador** | Sí | Panel con indicadores, gestión de pedidos (confirmar, entregar, cancelar), productos con foto, compras a proveedores (suman stock), categorías, proveedores, clientes y usuarios. |

### Ciclo de un pedido

```
Comprador confirma el carrito ──► PENDIENTE (stock reservado)
                                     │ Admin: "Confirmar pago"
                                     ▼
                                 CONFIRMADO ──► Admin: "Entregado" ──► ENTREGADO (venta cerrada)
   PENDIENTE o CONFIRMADO ──► "Cancelar" ──► CANCELADO (el stock vuelve)
```

---

## 1. Requisitos

| Herramienta | Notas |
|---|---|
| Visual Studio 2022 17.14+ o 2026 | Con las cargas de trabajo **Desarrollo de ASP.NET y web** y **Desarrollo de interfaz de usuario de aplicaciones multiplataforma de .NET (MAUI)** |
| .NET SDK 10 | `dotnet --version` |
| SQL Server LocalDB / Express | Para la base local |
| Emulador de Android | Visual Studio → *Herramientas → Android → Administrador de dispositivos Android* (por ejemplo Pixel 7, API 34) |
| `dotnet-ef` | `dotnet tool install --global dotnet-ef` |

## 2. Base de datos (Code-First)

> Si ya habías creado la base con la versión anterior (con rol Vendedor), borrá la carpeta `TechHub.Api/Migrations` y ejecutá `dotnet ef database drop`. El modelo cambió: pedidos, compradores y la relación Usuario-Cliente.

```bash
cd TechHub.Api
dotnet ef migrations add InitialCreate
```

Al iniciar, la API aplica las migraciones y crea el administrador inicial **admin / Admin123!**.

### Datos de demostración

Con `"Seed:DatosDemo": true` (activado por defecto), la API carga sola al arrancar:
- 2 proveedores.
- 16 productos con imagen (procesadores, placas de video, motherboards, RAM, SSD y fuentes), con stock cargado mediante compras (Ingresos). Uno queda sin stock y varios con stock bajo, para mostrar las alertas.
- Un comprador de prueba, **comprador@techhub.local / Comprador123!**, con 3 pedidos: uno entregado, uno confirmado y uno pendiente.

La carga es idempotente: solo agrega los productos que todavía no existen. Las imágenes de demo están en `wwwroot/demo` y son ilustraciones genéricas. Los precios son orientativos.

## 3. Ejecutar en la PC con el emulador

1. **API:** en Visual Studio elegí el proyecto `TechHub.Api` con el perfil **http** y ejecutalo. Queda en `http://localhost:5180` y Swagger en `http://localhost:5180/swagger`.
2. **App:** en `TechHub.App/Config/ApiConfig.cs` dejá `UsarServidorLocal = true`. La app usa `http://10.0.2.2:5180`, que es tu PC vista desde el emulador.
3. Elegí `TechHub.App`, seleccioná el emulador de Android y presioná F5.

Para tener las dos en marcha a la vez: *clic derecho en la solución → Configurar proyectos de inicio → Varios proyectos de inicio → Api y App en "Iniciar"*.

## 4. Generar el APK para el teléfono

1. Publicá la API en MonsterASP (sección 6).
2. En `ApiConfig.cs` poné `UsarServidorLocal = false` y en `UrlProduccion` la URL de tu sitio (https).
3. Ejecutá:
   ```bash
   cd TechHub.App
   dotnet publish -f net10.0-android -c Release
   ```
4. El APK queda en `TechHub.App/bin/Release/net10.0-android/publish/com.techhub.app-Signed.apk`. Pasalo al teléfono y habilitá *Instalar apps desconocidas*.

## 5. Arquitectura

```
TechHub.Api/
├── Controllers/   Auth, Productos, Categorias, Proveedores, Clientes, Ingresos, Pedidos, Usuarios, Dashboard
├── Models/        Entidades (7 tablas) + Roles + EstadosPedido
├── DTOs/          Contratos de entrada/salida con validaciones
├── Data/          AppDbContext (Fluent API, índices, CHECK, relaciones) y DbSeeder
├── Services/      TokenService (JWT), MovimientoService (stock transaccional), ImagenService
├── Common/        Paginación, manejo global de errores (ProblemDetails)
└── wwwroot/uploads  Fotos de productos

TechHub.App/
├── Config/        ApiConfig (URL de la API)
├── Models/        DTOs del lado cliente + formato de moneda/fecha
├── Services/      ApiClient (HTTP + Bearer), SesionService (SecureStorage), CarritoService
├── ViewModels/    MVVM: Tienda, Admin y ABM genérico
├── Views/         17 pantallas XAML
└── AppShell       Dos barras de pestañas: tienda (visitante/comprador) y admin
```

### Modelo de datos (7 tablas)

| Tabla | Descripción | Relaciones |
|---|---|---|
| Categorias | Clasificación del stock | 1 → N Productos |
| Productos | Componente: precio, stock, stock mínimo, imagen | N → 1 Categoría |
| Proveedores | Distribuidores mayoristas (CUIT único) | 1 → N Ingresos |
| Clientes | Personas o empresas (DNI/CUIT único) | 1 → N Salidas · 1 → 0..1 Usuario |
| Ingresos | Compra a proveedor: una fila por producto | N → 1 Producto, Proveedor, Usuario |
| Salidas | Venta: una fila por producto, agrupadas por `CodigoPedido` con `Estado` | N → 1 Producto, Cliente, Usuario |
| Usuarios | Cuentas: Admin o Comprador, contraseña hasheada | 0..1 → 1 Cliente · 1 → N Ingresos y Salidas |

Decisiones de diseño:
- **Precio congelado:** `Salidas.PrecioUnitario` guarda el precio del momento de la compra, así un cambio de precio no altera el historial.
- **Trazabilidad:** el stock no se edita a mano. Sube con Ingresos y baja con Salidas.
- **Stock nunca negativo:** cada línea del pedido ejecuta `UPDATE ... SET Stock = Stock - @n WHERE Id = @id AND Stock >= @n` dentro de una transacción. Si un producto no alcanza, se revierte todo el pedido y la API responde `409 Conflict`. Además, la base tiene `CHECK (Stock >= 0)`.
- **Bajas lógicas:** productos, clientes, proveedores y usuarios se desactivan, no se borran (`ON DELETE RESTRICT`).

## 6. Seguridad

- **JWT firmado con HMAC-SHA256.** El token **no está encriptado**: su contenido se puede leer, pero no modificar sin invalidar la firma. Se validan emisor, audiencia, firma y vencimiento.
- **En la app,** el token se guarda en `SecureStorage` (Android Keystore) y se envía como `Authorization: Bearer ...`.
- **Contraseñas** hasheadas con `PasswordHasher` de ASP.NET Core Identity (PBKDF2 con salt).
- **401** = sin token o token inválido/vencido (la app cierra la sesión). **403** = token válido pero rol insuficiente (por ejemplo, un comprador que pide el dashboard).
- **Tráfico:** HTTP sin cifrar está permitido solo hacia `10.0.2.2` (emulador). Todo lo demás exige HTTPS (`network_security_config.xml`).

### Permisos por endpoint

| Recurso | Visitante | Comprador | Admin |
|---|---|---|---|
| GET productos / categorías | ✅ | ✅ | ✅ (más inactivos) |
| POST `/api/auth/registro`, `/api/auth/login` | ✅ | — | — |
| GET/PUT `/api/auth/perfil` | — | ✅ | ✅ |
| POST `/api/pedidos` | — | ✅ (queda Pendiente) | ✅ (venta en local, requiere `clienteId`) |
| GET `/api/pedidos/mios`, POST `/{codigo}/cancelar` | — | ✅ | — |
| GET `/api/pedidos`, PUT `/{codigo}/estado` | — | — | ✅ |
| Productos (ABM e imagen), categorías, proveedores, clientes, ingresos, usuarios, dashboard | — | — | ✅ |

## 7. Paginación

Todos los listados devuelven:

```json
{ "items": [ ... ], "pagina": 1, "tamanio": 10, "totalItems": 37, "totalPaginas": 4 }
```

El tamaño máximo es 50 y la consulta se resuelve en SQL Server con `OFFSET/FETCH`. En la app, los listados usan **scroll infinito**: al acercarse al final de la lista, piden la página siguiente.

## 8. Imágenes

`POST /api/productos/{id}/imagen` (multipart, campo `archivo`) hace lo siguiente:
1. Valida tamaño (máx. 5 MB), extensión y MIME (JPG, PNG o WEBP).
2. Verifica la firma binaria del archivo.
3. Lo guarda como `wwwroot/uploads/{GUID}.ext`.
4. Registra la ruta relativa en `Productos.ImagenUrl` y borra la imagen anterior.

En la app, el admin elige la foto desde la galería del teléfono.

## 9. Despliegue de la API en MonsterASP.NET

1. Crear el **sitio** y la **base MSSQL** en el panel. Copiar la *connection string*.
2. Copiar `appsettings.Production.example.json` como `appsettings.Production.json` y completar la connection string, una `Jwt:Key` larga y aleatoria y la contraseña del admin. Este archivo no se sube a GitHub.
3. Publicar desde Visual Studio: *clic derecho en TechHub.Api → Publicar → Importar perfil* (el `.publishsettings` se descarga del panel).
4. La API aplica las migraciones en la base remota al iniciar.
5. Verificar que `wwwroot/uploads` tenga permiso de escritura y dejar desmarcado *Quitar archivos adicionales en el destino*.

## 10. Pruebas

`TechHub.Api/TechHub.Api.http` contiene el recorrido completo: login, 401 sin token, alta de producto, compra a proveedor, registro de comprador, pedido, 409 por stock insuficiente, 403 por rol y cambio de estado.

**Recorrido sugerido en la app (para la demo):**
1. Admin: crear producto con foto → registrar compra (stock +5).
2. Cerrar sesión → como visitante, ver el catálogo y agregar al carrito.
3. Confirmar → crear cuenta de comprador → confirmar pedido (queda Pendiente y el stock se reserva).
4. Admin: Pedidos → confirmar pago → marcar entregado. El panel muestra la facturación.
