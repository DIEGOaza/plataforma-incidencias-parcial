// Recibe en tiempo real el evento IncidenciaActualizada ({ id, estado }) por PieHost y/o SignalR,
// actualiza la tabla sin recargar la página y, al reconectar, vuelve a consultar el estado vigente.
(function () {
    "use strict";

    const config = JSON.parse(document.getElementById("config-tiempo-real").textContent);
    const tabla = document.getElementById("tabla-incidencias");
    const cuerpo = tabla.querySelector("tbody");
    const sinIncidencias = document.getElementById("sin-incidencias");
    const avisos = document.getElementById("notificaciones-incidencias");

    // Un mismo evento puede llegar por PieHost y por SignalR: se avisa una sola vez.
    const ultimosAvisos = new Map();

    function formatearFecha(valor) {
        const fecha = new Date(valor);
        if (isNaN(fecha)) return "";
        const dos = n => String(n).padStart(2, "0");
        return `${dos(fecha.getDate())}/${dos(fecha.getMonth() + 1)}/${fecha.getFullYear()} ${dos(fecha.getHours())}:${dos(fecha.getMinutes())}`;
    }

    function resaltar(fila) {
        fila.classList.add("table-warning");
        setTimeout(() => fila.classList.remove("table-warning"), 4000);
    }

    function mostrarAviso(texto) {
        const aviso = document.createElement("div");
        aviso.className = "alert alert-info alert-dismissible fade show";
        aviso.setAttribute("role", "status");
        aviso.appendChild(document.createTextNode(texto));

        const cerrar = document.createElement("button");
        cerrar.type = "button";
        cerrar.className = "btn-close";
        cerrar.setAttribute("data-bs-dismiss", "alert");
        cerrar.setAttribute("aria-label", "Cerrar");
        aviso.appendChild(cerrar);

        avisos.prepend(aviso);
        setTimeout(() => aviso.remove(), 8000);
    }

    // Botón "Cerrar" (formulario POST con token antiforgery), solo para incidencias Abiertas.
    function pintarAcciones(celda, id, estado) {
        celda.replaceChildren();
        if (estado !== "Abierta") return;

        const form = document.createElement("form");
        form.method = "post";
        form.action = `${config.cerrarUrl}/${encodeURIComponent(id)}`;
        form.className = "d-inline";

        const token = document.querySelector('#antiforgery-cerrar input[name="__RequestVerificationToken"]');
        if (token) form.appendChild(token.cloneNode());

        const boton = document.createElement("button");
        boton.type = "submit";
        boton.className = "btn btn-sm btn-outline-danger";
        boton.textContent = "Cerrar";
        form.appendChild(boton);

        celda.appendChild(form);
    }

    // Reconstruye la tabla con el estado vigente del servidor (textContent evita inyectar HTML).
    function pintarListado(incidencias) {
        cuerpo.replaceChildren(...incidencias.map(i => {
            const fila = document.createElement("tr");
            fila.dataset.incidenciaId = i.id;
            [["estacion", i.estacion], ["descripcion", i.descripcion], ["estado", i.estado], ["fecha", formatearFecha(i.fechaReporte)]]
                .forEach(([campo, valor]) => {
                    const td = document.createElement("td");
                    td.dataset.campo = campo;
                    td.textContent = valor ?? "";
                    fila.appendChild(td);
                });
            const acciones = document.createElement("td");
            acciones.dataset.campo = "acciones";
            pintarAcciones(acciones, i.id, i.estado);
            fila.appendChild(acciones);
            return fila;
        }));

        tabla.classList.toggle("d-none", incidencias.length === 0);
        sinIncidencias.classList.toggle("d-none", incidencias.length > 0);
    }

    function actualizarVacio() {
        const hayFilas = cuerpo.querySelector("tr") !== null;
        tabla.classList.toggle("d-none", !hayFilas);
        sinIncidencias.classList.toggle("d-none", hayFilas);
    }

    // Con una búsqueda activa solo se quitan las filas que ya no están abiertas
    // (no se reemplazan los resultados de Algolia por el listado general).
    function aplicarEstadoVigente(abiertas) {
        if (!config.busquedaActiva) {
            pintarListado(abiertas);
            return;
        }
        const idsAbiertas = new Set(abiertas.map(i => String(i.id)));
        cuerpo.querySelectorAll("tr[data-incidencia-id]").forEach(fila => {
            if (!idsAbiertas.has(fila.dataset.incidenciaId)) fila.remove();
        });
        actualizarVacio();
    }

    let refrescando = null;
    function refrescarEstado() {
        // Si ya hay una consulta en curso, se reutiliza.
        refrescando ??= fetch(config.estadoUrl, { credentials: "same-origin", headers: { Accept: "application/json" } })
            .then(r => {
                if (!r.ok) throw new Error(`HTTP ${r.status}`);
                return r.json();
            })
            .then(aplicarEstadoVigente)
            .catch(err => console.warn("No se pudo consultar el estado vigente de las incidencias.", err))
            .finally(() => { refrescando = null; });
        return refrescando;
    }

    // Evento IncidenciaActualizada: { id, estado }
    function procesar(evento) {
        if (!evento || evento.id == null) return;

        const fila = cuerpo.querySelector(`tr[data-incidencia-id="${Number(evento.id)}"]`);
        if (evento.estado !== "Abierta") {
            // El listado solo muestra incidencias abiertas: la cerrada se quita sin recargar.
            if (fila) fila.remove();
            actualizarVacio();
        } else if (!fila) {
            // Incidencia abierta que la pantalla aún no conoce: se trae el estado vigente.
            refrescarEstado();
        } else {
            fila.querySelector('[data-campo="estado"]').textContent = evento.estado;
            const acciones = fila.querySelector('[data-campo="acciones"]');
            if (acciones) pintarAcciones(acciones, evento.id, evento.estado);
            resaltar(fila);
        }

        const clave = `${evento.id}:${evento.estado}`;
        const ahora = Date.now();
        if (ahora - (ultimosAvisos.get(clave) ?? 0) > 3000) {
            ultimosAvisos.set(clave, ahora);
            mostrarAviso(`Incidencia #${evento.id} actualizada: ${evento.estado ?? ""}`);
        }
    }

    // --- SignalR (WebSockets con reconexión automática) ---
    const conexion = new signalR.HubConnectionBuilder()
        .withUrl(config.hubUrl)
        .withAutomaticReconnect()
        .build();

    conexion.on(config.evento, procesar);
    // Al recuperar la conexión se pudieron perder eventos: se consulta el estado vigente.
    conexion.onreconnected(() => refrescarEstado());

    (function iniciar(esReintento) {
        conexion.start()
            .then(() => { if (esReintento) refrescarEstado(); })
            .catch(err => {
                console.warn("SignalR: no se pudo conectar, reintentando en 5 s.", err);
                setTimeout(() => iniciar(true), 5000);
            });
    })(false);

    // --- PieHost: WebSocket al canal configurado (WebSocketUrl, solo con la ApiKey pública) ---
    if (config.pieHost) {
        const url = config.pieHost.url;
        let yaConecto = false;

        (function conectarPieHost(espera) {
            const ws = new WebSocket(url);
            ws.onopen = () => {
                espera = 1000;
                // Reconexión: consultar el estado vigente por si se perdieron eventos.
                if (yaConecto) refrescarEstado();
                yaConecto = true;
            };
            ws.onmessage = e => {
                try {
                    let datos = JSON.parse(e.data);
                    if (typeof datos === "string") datos = JSON.parse(datos);
                    if (datos?.event === config.evento) procesar(datos.data);
                } catch {
                    // Mensajes que no son eventos de incidencia (p. ej. avisos del sistema) se ignoran.
                }
            };
            ws.onclose = () => setTimeout(() => conectarPieHost(Math.min(espera * 2, 30000)), espera);
        })(1000);
    }
})();
