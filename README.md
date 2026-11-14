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

**API publicada:** <http://apiintegrador.runasp.net/swagger> (estado del servicio: `/api/estado`).

**Documentación:** [Diagrama Entidad-Relación](docs/DER_TechHub.png) · [Casos de uso (diagrama y especificación)](docs/CasosDeUso.md)

## Perfiles de la app

| Perfil | Login | Qué puede hacer |
|---|---|---|
| **Visitante** | No | Ver el catálogo con fotos, buscar, filtrar por categoría y ordenar por precio. Ver el detalle de cada producto y armar el carrito. |
| **Comprador** | Sí (se registra desde la app) | Lo mismo que el visitante, más confirmar compras, ver *Mis pedidos*, cancelar un pedido pendiente y editar su perfil. |
| **Administrador** | Sí | Panel con indicadores, gestión de pedidos (confirmar, entregar, cancelar), productos con foto, compras a proveedores (suman stock), categorías, proveedores, clientes y usuarios. |

### Funciones destacadas de la app

- **Compra ágil:** botón **+** en cada tarjeta del catálogo, contador de unidades en la pestaña *Carrito (n)* y avisos breves (toast) en lugar de ventanas emergentes.
- **Catálogo:** carrusel de **más vendidos**, filtro por categoría y por **rango de precio**, orden por nombre, precio, más vendidos o novedades, y sección **"También te puede interesar"** en el detalle.
- **Seguimiento de pedidos:** **línea de tiempo** Pendiente → Confirmado → Entregado y botón **"Volver a comprar"**, que carga el carrito con el stock y los precios actuales.
- **Panel del admin:** **gráfico de barras** con la facturación de los últimos 7 días (dibujado con Microsoft.Maui.Graphics) y **ranking de productos más vendidos**.

- **Pago por transferencia:** el carrito muestra el alias, y el pedido trae los datos para transferir con botón **copiar**. El comprador **sube el comprobante** (foto o PDF) desde la app y el admin lo revisa: **aprueba** o **rechaza** el pago, indicando el motivo.
- **Reportes en PDF:** comprobante de pedido, **inventario valorizado** y **ventas por período**. Se pueden ver, compartir (WhatsApp, Drive…) o **enviar por email** desde Gmail.
- **Emails automáticos:** al crear el pedido, con los datos de transferencia, y al confirmar el pago, con el comprobante en PDF adjunto.
- **Diseño claro minimalista:** fondo blanco, botones negros tipo píldora, tarjetas con borde fino y tipografía grande.

### Ciclo de un pedido (pago por transferencia)

```
Comprador confirma el carrito ──► ESPERANDO PAGO (Pendiente, stock reservado) ── email con alias/CBU
        │ sube el comprobante (foto o PDF)
        ▼
PAGO EN REVISIÓN (EnRevision) ── Admin: "Rechazar" (con motivo) ──► vuelve a ESPERANDO PAGO
        │ Admin: "Aprobar pago"
        ▼
PAGO CONFIRMADO (Confirmado) ── email con el comprobante PDF ──► Admin: "Entregado" ──► ENTREGADO
Antes de entregarse, el pedido se puede CANCELAR y el stock vuelve.
```

La facturación del panel y de los reportes cuenta solo los pedidos **confirmados o entregados**.

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
- 17 productos con foto (procesadores, placas de video, motherboards, RAM, SSD y fuentes), con stock cargado mediante compras (Ingresos). Uno queda sin stock y varios con stock bajo, para mostrar las alertas.
- Un comprador de prueba, **comprador@techhub.local / Comprador123!**, con 3 pedidos: uno entregado, uno confirmado y uno pendiente.

La carga es idempotente: solo agrega los productos que todavía no existen (por nombre o por imagen). Las fotos de producto están en `wwwroot/demo` (PNG de 600×600 con fondo blanco) y se usan solo con fines académicos. Si se reemplazan, hay que subir `VersionFotosDemo` en `TechHub.App/Config/ApiConfig.cs` para que la app no muestre las anteriores desde la caché. Los precios son orientativos.

### Migración de pagos y comprobantes

Esta versión agrega a `Salidas` las columnas `ComprobanteArchivo`, `ComprobanteFecha` y `MotivoRechazo`:

```bash
cd TechHub.Api
dotnet ef migrations add PagosYComprobantes
```

La API aplica la migración sola al iniciar.

### Configurar pagos y Gmail

- **Datos de transferencia:** completar la sección `Pagos` de `appsettings.json` (alias, CBU/CVU, titular, CUIT y banco).
- **Gmail:**
  1. En la cuenta de Google, activar la **verificación en 2 pasos**.
  2. Entrar a <https://myaccount.google.com/apppasswords> y crear una **contraseña de aplicación** (16 caracteres).
  3. Copiar `appsettings.Local.example.json` como `appsettings.Local.json` y poner el Gmail y esa contraseña. Para producción, ponerlos en la sección `Email` de `appsettings.Production.json`.
  4. Los dos archivos están en `.gitignore`: **la contraseña nunca se sube a GitHub**.

Sin esta configuración la app funciona igual: los emails automáticos se omiten (queda registrado en el log) y el envío manual avisa que no está configurado.

Los comprobantes de pago se guardan en `TechHub.Api/App_Data/comprobantes`, fuera de `wwwroot`, así que **no son públicos**. Solo se descargan por la API, y solo el dueño del pedido o un admin pueden verlos.

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
| GET productos (filtros: `buscar`, `categoriaId`, `precioMin`, `precioMax`, `orden=nombre\|precio\|precio_desc\|vendidos\|nuevos`), `/api/productos/{id}/relacionados`, categorías | ✅ | ✅ | ✅ (más inactivos) |
| POST `/api/auth/registro`, `/api/auth/login` | ✅ | — | — |
| GET/PUT `/api/auth/perfil` | — | ✅ | ✅ |
| POST `/api/pedidos` | — | ✅ (queda Pendiente) | ✅ (venta en local, requiere `clienteId`) |
| GET `/api/pedidos/mios`, POST `/{codigo}/cancelar` | — | ✅ | — |
| GET `/api/pedidos`, PUT `/{codigo}/estado` | — | — | ✅ |
| Productos (ABM e imagen), categorías, proveedores, clientes, ingresos, usuarios | — | — | ✅ |
| Dashboard: `/resumen`, `/ventas-diarias?dias=7`, `/top-productos?cantidad=5` | — | — | ✅ |
| GET `/api/pagos/transferencia` (alias, CBU, titular) | ✅ | ✅ | ✅ |
| POST `/api/pedidos/{codigo}/comprobante` (multipart, campo `archivo`) | — | ✅ (su pedido) | — |
| GET `/api/pedidos/{codigo}/comprobante` | — | ✅ (su pedido) | ✅ |
| POST `/api/pedidos/{codigo}/rechazar-pago` `{ motivo }` | — | — | ✅ |
| GET `/api/reportes/pedido/{codigo}` (PDF) | — | ✅ (su pedido) | ✅ |
| GET `/api/reportes/inventario`, `/api/reportes/ventas?desde=&hasta=` (PDF) | — | — | ✅ |
| POST `/api/reportes/enviar` `{ tipo, email, codigo, desde, hasta }` | — | ✅ (su pedido, a su email) | ✅ |

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
