# 🍓 QuillenBot — Bot de Gestión de Pedidos y Proveedores

Bot de Telegram para **Quillen Berries** desarrollado en C# (.NET 8).  
Maneja dos flujos diferenciados: **clientes mayoristas** y **proveedores de servicios**.

---

## 📁 Estructura del Proyecto

```
QuillenBot/
├── Program.cs                  ← Entrada principal, Long Polling
├── appsettings.json            ← Configuración (tokens, paths)
├── QuillenBot.csproj
├── Config/
│   └── AppConfig.cs            ← Clases de configuración
├── Models/
│   └── Models.cs               ← PedidoCliente, RegistroProveedor, Catalogo, etc.
├── Services/
│   ├── ExcelService.cs         ← Lectura/escritura ClosedXML
│   ├── GeminiService.cs        ← Clasificación y NLP con Google Gemini
│   └── SessionManager.cs      ← Estado de conversaciones en memoria
└── Handlers/
    └── MessageHandler.cs       ← Toda la lógica de flujo del bot
```

---

## 🚀 Configuración inicial

### 1. Crear el Bot en Telegram
1. Abrí Telegram y hablá con **@BotFather**
2. Enviá `/newbot` y seguí los pasos
3. Copiá el **Token** que te da

### 2. Obtener tu Chat ID (para aprobaciones)
1. Hablá con **@userinfobot** en Telegram
2. Te devuelve tu `id` — ese es tu `ApproverChatId`

### 3. Obtener API Key de Gemini
1. Entrá a [https://aistudio.google.com/apikey](https://aistudio.google.com/apikey)
2. Creá una clave gratuita

### 4. Configurar `appsettings.json`
```json
{
  "BotConfiguration": {
    "BotToken": "1234567890:ABCdef...",
    "ApproverChatId": 123456789,
    "ExcelFilePath": "quillen_pedidos.xlsx"
  },
  "GeminiConfiguration": {
    "ApiKey": "AIzaSy...",
    "Model": "gemini-1.5-flash"
  }
}
```

> ⚠️ **NUNCA subas `appsettings.json` con tus tokens a Git.** Agregalo al `.gitignore`.

---

## ▶️ Ejecutar

```bash
cd QuillenBot
dotnet restore
dotnet run
```

---

## 📊 Excel generado (`quillen_pedidos.xlsx`)

Se crea automáticamente con **3 hojas**:

| Hoja | Descripción |
|------|-------------|
| `Pedidos` | Resumen de cada pedido (empresa, total, estado) |
| `Items_Pedidos` | Detalle de productos por pedido |
| `Proveedores` | Datos de proveedores registrados |

### Estados de pedido:
- 🟡 **Pendiente** — esperando aprobación
- 🟢 **Aprobado** — confirmado por la empresa
- 🔴 **Rechazado** — no aprobado

---

## 🔄 Flujo de uso

### Cliente mayorista:
```
/start
→ Selecciona "Soy cliente"
→ Nombre → Empresa → Teléfono → Email
→ Elige productos del catálogo + cantidades
→ Dirección de entrega
→ Confirma pedido
→ Quillen recibe notificación con botones [Aprobar] [Rechazar]
→ Cliente recibe confirmación
```

### Proveedor:
```
/start
→ Selecciona "Soy proveedor"
→ Nombre → Empresa → Teléfono → Email
→ Servicio ofrecido + descripción
→ Confirma
→ Quillen recibe notificación informativa
→ Datos guardados en Excel
```

---

## 🛒 Catálogo actual

| Producto | Precio/kg |
|----------|-----------|
| Frutilla | $850 |
| Arándanos | $1.200 |
| Moras | $950 |
| Frambuesas | $1.100 |
| Mango | $700 |
| Piña | $600 |
| Maracuyá | $800 |

Para modificar precios o agregar productos: editá `Catalogo.Productos` en `Models/Models.cs`.

---

## 🤖 Rol de Gemini (IA)

- **Clasificación inicial**: Determina si el mensaje es de cliente o proveedor
- **Interpretación de productos**: Entiende variantes (ej: "fresas" → "Frutilla", "blueberries" → "Arándanos")

Si Gemini no está disponible, el bot funciona igualmente con clasificación manual via botones.

---

## 📌 Notas para el TFI

- **RPA implementado**: n8n fue reemplazado por C# puro con control total
- **IA/ML**: Google Gemini para NLP y clasificación de usuarios
- **BPM**: Flujo definido por `ConversationStep` enum (equivale al proceso TO-BE)
- **Integración**: Excel como base de datos, extensible a ERP/CRM via API REST
- **Aprobación humana**: Loop en el proceso donde el empleado de Quillen aprueba/rechaza

---

## 🔧 Extensiones futuras sugeridas

1. **Persistencia real**: Reemplazar el `SessionManager` en memoria por una base de datos SQLite o PostgreSQL
2. **ERP**: Conectar `ConsultarStock()` a ERPNext via REST API
3. **Webhook**: Para producción en servidor, cambiar Long Polling por Webhook (ver `SetWebhookAsync`)
4. **Notificaciones por email**: Usar SMTP o SendGrid al aprobar un pedido
