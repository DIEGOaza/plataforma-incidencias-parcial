// Recibe en tiempo real los eventos de incidencias (SignalR y, si está configurado, PieHost)
// y actualiza la tabla y muestra un aviso sin recargar la página.
(function () {
    "use strict";

    const config = JSON.parse(document.getElementById("config-tiempo-real").textContent);
    const tabla = document.getElementById("tabla-incidencias");
    const cuerpo = tabla.querySelector("tbody");
    const sinIncidencias = document.getElementById("sin-incidencias");
    const avisos = document.getElementById("notificaciones-incidencias");

    // Un mismo evento puede llegar por SignalR y por PieHost: se procesa una sola vez.
    const eventosProcesados = new Set();

    const textos = {
        nueva: "Nueva incidencia registrada",
        abierta: "Incidencia marcada como Abierta",
        actualizada: "Incidencia actualizada"
    };

    function formatearFecha(valor) {
        const fecha = new Date(valor);
        if (isNaN(fecha)) return "";
        const dos = n => String(n).padStart(2, "0");
        return `${dos(fecha.getDate())}/${dos(fecha.getMonth() + 1)}/${fecha.getFullYear()} ${dos(fecha.getHours())}:${dos(fecha.getMinutes())}`;
    }

    function crearCelda(campo) {
        const td = document.createElement("td");
        td.dataset.campo = campo;
        return td;
    }

    function actualizarTabla(n) {
        let fila = cuerpo.querySelector(`tr[data-incidencia-id="${Number(n.id)}"]`);
        if (!fila) {
            fila = document.createElement("tr");
            fila.dataset.incidenciaId = n.id;
            ["estacion", "descripcion", "estado", "fecha"].forEach(c => fila.appendChild(crearCelda(c)));
            cuerpo.prepend(fila);
        }

        // textContent evita inyectar HTML recibido del servidor.
        fila.querySelector('[data-campo="estacion"]').textContent = n.estacion ?? "";
        fila.querySelector('[data-campo="descripcion"]').textContent = n.descripcion ?? "";
        fila.querySelector('[data-campo="estado"]').textContent = n.estado ?? "";
        fila.querySelector('[data-campo="fecha"]').textContent = formatearFecha(n.fechaReporte);

        fila.classList.add("table-warning");
        setTimeout(() => fila.classList.remove("table-warning"), 4000);

        tabla.classList.remove("d-none");
        sinIncidencias.classList.add("d-none");
    }

    function mostrarAviso(n) {
        const aviso = document.createElement("div");
        aviso.className = "alert alert-info alert-dismissible fade show";
        aviso.setAttribute("role", "status");

        const titulo = document.createElement("strong");
        titulo.textContent = (textos[n.tipo] ?? "Cambio en incidencia") + ": ";
        aviso.appendChild(titulo);
        aviso.appendChild(document.createTextNode(`${n.descripcion ?? ""}${n.estacion ? " (" + n.estacion + ")" : ""}`));

        const cerrar = document.createElement("button");
        cerrar.type = "button";
        cerrar.className = "btn-close";
        cerrar.setAttribute("data-bs-dismiss", "alert");
        cerrar.setAttribute("aria-label", "Cerrar");
        aviso.appendChild(cerrar);

        avisos.prepend(aviso);
        setTimeout(() => aviso.remove(), 8000);
    }

    function procesar(n) {
        if (!n || n.id == null) return;
        if (n.eventoId) {
            if (eventosProcesados.has(n.eventoId)) return;
            eventosProcesados.add(n.eventoId);
        }
        actualizarTabla(n);
        mostrarAviso(n);
    }

    // --- SignalR (WebSockets con reconexión automática) ---
    const conexion = new signalR.HubConnectionBuilder()
        .withUrl(config.hubUrl)
        .withAutomaticReconnect()
        .build();

    conexion.on(config.evento, procesar);

    (function iniciar() {
        conexion.start().catch(err => {
            console.warn("SignalR: no se pudo conectar, reintentando en 5 s.", err);
            setTimeout(iniciar, 5000);
        });
    })();

    // --- PieHost (opcional): WebSocket directo al canal con la ApiKey pública ---
    if (config.pieHost) {
        const { clusterId, apiKey, canal } = config.pieHost;
        const url = `wss://${encodeURIComponent(clusterId)}.piesocket.com/v3/${encodeURIComponent(canal)}?api_key=${encodeURIComponent(apiKey)}`;

        (function conectarPieHost(espera) {
            const ws = new WebSocket(url);
            ws.onopen = () => { espera = 1000; };
            ws.onmessage = e => {
                try {
                    let datos = JSON.parse(e.data);
                    if (typeof datos === "string") datos = JSON.parse(datos);
                    procesar(datos.message ?? datos);
                } catch {
                    // Mensajes que no son eventos de incidencia (p. ej. avisos del sistema) se ignoran.
                }
            };
            ws.onclose = () => setTimeout(() => conectarPieHost(Math.min(espera * 2, 30000)), espera);
        })(1000);
    }
})();
