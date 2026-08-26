# Programación 7 — Semana 7: Programación Paralela II (Multiprocesamiento y Multihilo)

## Resumen del contenido

El documento abre con la cita de Kent Beck: "Hazlo funcionar, hazlo bien, hazlo rápido", que enmarca el enfoque práctico del contenido (portada, sin valor de conocimiento adicional).

## Conceptos clave

### Multiprocesamiento

**Definición:** Implica ejecutar múltiples procesos simultáneamente.

**Explicación:** Cada proceso tiene su propio espacio de memoria, recursos y entorno de ejecución. Básicamente, un proceso es una instancia completa **aislada** de las demás.

**Características:**
- Cada proceso posee sus propios: Code, Data, Files, Registers, Counter, Stack y al menos un Thread.
- La comunicación entre procesos requiere mecanismos explícitos: tuberías (pipes), colas, memoria compartida o sockets.
- La creación del proceso y el cambio de contexto (context switch) tienen mayor sobrecarga (overhead) que los hilos.

**Relaciones:**
- Se contrasta directamente con el concepto de Multihilo (comparten CPU/paralelismo pero difieren en aislamiento y costo).
- Es la base conceptual del Módulo Cluster de Node.js.

### Multihilo (Multithreading)

**Definición:** Un proceso único puede crear múltiples hilos que pueden ejecutarse concurrentemente con el mismo espacio de memoria.

**Explicación:** Un hilo representa una secuencia independiente de instrucciones y comparte la misma memoria, manejadores de archivos y otros recursos del sistema dentro de un proceso.

**Características:**
- Todos los hilos de un proceso comparten Code, Data y Files, pero cada hilo tiene sus propios Registers, Counter y Stack (ver diagrama "Multihilo de Procesador Único").
- La comunicación entre hilos es directa (acceden al mismo espacio de memoria), pero necesita **sincronización** para prevenir condiciones de carrera (race conditions) y corrupción de datos.
- Los hilos son unidades de ejecución ligeras, con sobrecarga mínima para creación y cambio de contexto.

**Relaciones:**
- Es la base conceptual de los Worker Threads en Node.js y de los Web Workers en el navegador.
- Se compara con Multiprocesamiento: mayor eficiencia en comunicación y menor costo de recursos, a cambio de menor aislamiento (riesgo de condiciones de carrera).

### Node.js de un solo hilo (Event Loop)

**Definición:** Node.js opera en un modelo de bucle de eventos (event loop) de un solo hilo para la ejecución de JavaScript.

**Explicación:** El bucle de eventos maneja operaciones concurrentes mediante E/S (I/O) asíncrona y no bloqueante.

**Características (según diagrama del Event Loop):**
- El Event Loop es de un solo hilo ("Single thread").
- Recibe continuamente Requests, Async Operations, etc.
- Cuando debe procesar operaciones intensivas (File System, Computation, Heavy task), registra un callback ("register callback") y encola la tarea en una Queue de "Intensive Operations".
- Cuando la operación se completa ("operation complete"), se dispara el callback ("Trigger Callback") que retorna al flujo del Event Loop.

**Relaciones:**
- Esta naturaleza de un solo hilo es el motivo histórico que originó tanto el Módulo Cluster (2011) como los Worker Threads (2018).

### Condiciones de carrera (Race Conditions)

**Definición:** No se define explícitamente en el documento, pero se menciona como riesgo derivado de la comunicación directa entre hilos que comparten memoria.

**Explicación:** Mencionada como algo que la sincronización entre hilos debe prevenir, junto con la corrupción de datos.

## Enfoques, métodos o herramientas

### Módulo Cluster (Node.js)

**Descripción:** Característica de Node.js que permite crear **procesos hijos**, aprovechando sistemas multinúcleo para mejorar el rendimiento y la confiabilidad de una aplicación.

**Arquitectura:** Maestro-trabajador (master-worker).
- Al ejecutar una aplicación se crea un único **proceso maestro**.
- El proceso maestro es responsable de generar y gestionar múltiples **procesos trabajadores** (workers).
- Diagrama: "Master Process" con flechas hacia varios "Child Process" (Child Process 1, Child Process 1 [repetido en el diagrama], Child Process N).

**Mecanismo de creación de trabajadores:**
- El método para generar trabajadores es `cluster.fork()`.
- Usualmente se crean tantos trabajadores como núcleos de CPU existan en la máquina.

**Compartición de puerto:**
- Todos los procesos trabajadores pueden compartir un único puerto de servidor TCP.
- Cuando se establece una nueva conexión, Node.js la distribuye a uno de los puertos/trabajadores disponibles.
- Estrategia de balanceo de carga por defecto: **round-robin simple**. El proceso maestro escucha en un puerto y distribuye cada nueva conexión al siguiente proceso trabajador en un bucle secuencial.

**Contexto histórico:**
- Debido a la naturaleza de un solo hilo de Node.js (lanzado en 2009), el Módulo Cluster (lanzado en 2011) fue la solución práctica para utilizar múltiples núcleos de CPU.
- Tiene una implementación simple usando procesos de bifurcación (fork).
- Node.js podía aprovechar las capacidades existentes del SO para la gestión de procesos sin cambiar su arquitectura.
- Debido a la popularidad de Node.js para construir servidores web, resolver esta limitación en entornos de producción era una necesidad urgente.

### Worker Threads (Node.js)

**Descripción:** Característica de Node.js que permite la **ejecución paralela de código JS**, ejecutando el código en hilos separados dentro del mismo proceso de Node.

**Objetivo:** Soportar operaciones JavaScript intensivas en CPU sin bloquear el bucle de eventos principal.

**Arquitectura (según diagrama):**
- Existe un "Main Event Loop".
- Desde el hilo principal se pueden crear nuevos workers mediante `new Worker()`.
- Cada worker thread posee sus propios componentes: una instancia de V8, y libuv (representados en el diagrama con íconos), además de "1 Event Loop" y "1 Thread" propios.

**Memoria:**
- Cada worker thread tiene su propia instancia de V8, bucle de eventos y heap de memoria.
- Sin embargo, existen dentro del mismo proceso y pueden compartir memoria mediante `SharedArrayBuffer`.

**Contexto histórico:**
- Inicialmente Node.js fue creado para soportar el entorno de ejecución de JavaScript, por lo que inherentemente usaba la arquitectura de bucle de eventos.
- **Entre 2010–2017:** el uso de Node.js creció y los desarrolladores encontraron limitaciones significativas en tareas intensivas de CPU que bloqueaban el bucle de eventos, tales como:
  - Procesamiento de imágenes/video
  - Operaciones matemáticas
  - Renderizado del lado del servidor
  - Integración de WebAssembly
- **Abril 2018 — Node.js 10.5.0:** la API de Worker Threads fue introducida como característica experimental, con la clase `Worker` y el paso de mensajes.
- **26 de septiembre de 2019 — Node.js 12.11.0:** Worker Threads marcado oficialmente como **estable**. Mejoras logradas desde el lanzamiento experimental hasta la versión estable:
  - La API de Worker Thread fue refinada basándose en la retroalimentación de la comunidad.
  - Mejor serialización de mensajes y manejo de errores.
  - Soporte de depuración mejorado.

### Web Workers (navegador)

**Descripción:** Característica del navegador que permite que el código JavaScript se ejecute en **hilos en segundo plano**, separados del hilo de ejecución principal. Esto permite **paralelismo verdadero** para aplicaciones web.

**Diagrama:** "Web Page" ↔ "Web Worker" (flecha bidireccional), con el Web Worker etiquetado como responsable de "Heavy Computations".

**Motivación (problema que resuelve):**
Dado que JavaScript es de un solo hilo por naturaleza, esto puede llevar a:
- Congelamiento de la UI para cálculos pesados.
- Interfaces que no responden.
- Subutilización de procesadores multinúcleo.
- Rendimiento limitado para tareas intensivas en cómputo.

Usando Web Workers:
- Los scripts se ejecutan en paralelo.
- Se mantiene la capacidad de respuesta de la UI.
- Se utilizan múltiples núcleos de CPU de manera efectiva.

**Características:**
- El hilo del worker se ejecuta en un contexto global diferente al hilo principal de la UI.
- Dado que trabaja en un hilo separado, un script de larga duración dentro de un worker nunca congelará la UI.
- Cada worker y el hilo principal tienen memoria separada: no pueden compartir variables directamente ni llamar funciones entre sí. La comunicación se maneja de forma **asíncrona a través de mensajes**.

**Consideraciones / limitaciones:**
1. Los Workers tienen costo de inicialización: creación de contexto y asignación de memoria.
2. Sobrecarga de comunicación: la serialización/deserialización de datos tiene costos.
3. Los Workers **no pueden** acceder a:
   - DOM (sin manipulación directa).
   - Objeto Window.
   - Referencias a objetos del hilo principal.
   - Local Storage / Session Storage.
4. Los Workers **pueden** acceder a:
   - Peticiones de red como `fetch()`, `XMLHttpRequest`.
   - Temporizadores.
   - Consola.
   - Websockets.
   - IndexedDB.
   - Algunas API Web como `crypto`, `performance`, `url`.
5. Al usar `postMessage()`, los datos enviados son **clonados**, no compartidos.

**Objetos Transferibles:**
En el caso de datos grandes, es posible usar Objetos Transferibles que transfieren la propiedad de los datos (en lugar de clonarlos), lo cual es más eficiente. Ejemplo: transferencia de un `ArrayBuffer` entre el hilo principal y el worker (ver sección de ejemplos).

## Procedimientos o procesos

### Comunicación Principal → Worker (Node.js Worker Threads)

1. El hilo principal crea la instancia `Worker` pasando la ruta/URL del archivo/módulo del worker (ej. `new Worker(new URL(import.meta.url), { workerData: {...} })`).
2. El worker recibe los datos iniciales mediante `workerData`.
3. El worker escucha mensajes adicionales mediante `parentPort` (aunque en el ejemplo del documento el envío inicial se hace vía `workerData`).
4. El worker procesa los datos y responde con `parentPort.postMessage(result)`.
5. El hilo principal escucha la respuesta con `worker.on('message', (result) => {...})`.

### Comunicación Principal → Web Worker (navegador)

1. El hilo principal crea la instancia `Worker` proporcionando la ruta del archivo JavaScript: `const myWorker = new Worker('heavy-task-worker.js');`.
2. El hilo principal envía mensajes al worker usando `worker.postMessage(dataToSend)`.
3. El worker escucha el evento `onmessage`. El ámbito global dentro del worker es `self`. Los datos enviados desde el hilo principal están disponibles en `event.data`.
4. El worker procesa los datos y responde usando `self.postMessage({...})`.
5. El hilo principal escucha el manejador del evento `onmessage` del worker respectivo: `myWorker.onmessage = function(event) {...}`, accediendo al resultado en `event.data`.

### Módulo Cluster — Flujo de inicialización

1. Se importan los módulos `node:cluster`, `node:http` y se obtiene el número de CPUs con `require('node:os').cpus().length`.
2. Se verifica si el proceso actual es el maestro con `cluster.isMaster`.
3. Si es el maestro: se registra su inicio (`console.log`), se generan tantos trabajadores como núcleos de CPU mediante `cluster.fork()` en un bucle, y se escucha el evento `exit` de los trabajadores para registrar cuando un trabajador muere.
4. Si es un trabajador (rama `else`): se crea un servidor HTTP con `http.createServer(...)` que comparte el puerto (`listen(8000)`), y se registra el inicio del trabajador con su PID.

## Ejemplos y casos

### Ejemplo — Módulo Cluster (código completo, dos diapositivas)

```js
const cluster = require('node:cluster');
const http = require('node:http');
const numCPUs = require('node:os').cpus().length;

if (cluster.isMaster) {
  console.log(`Maestro ${process.pid} está ejecutándose`);

  // Generar trabajadores para cada núcleo de CPU
  for (let i = 0; i < numCPUs; i++) {
    cluster.fork();
  }

  cluster.on('exit', (worker, code, signal) => {
    console.log(`Trabajador ${worker.process.pid} murió`);
  });
} else {
  // Los trabajadores comparten el puerto del servidor
  http.createServer((req, res) => {
    res.writeHead(200);
    res.end('Hola mundo\n');
  }).listen(8000);

  console.log(`Trabajador ${process.pid} iniciado`);
}
```

Puntos señalados por el documento sobre este ejemplo:
- Hay una inicialización del maestro.
- El método para generar trabajadores es `cluster.fork()`, usualmente tantos como núcleos de CPU existan.
- Todos los procesos trabajadores comparten un único puerto de servidor TCP; Node.js distribuye cada nueva conexión a uno de los trabajadores disponibles (round-robin simple).

### Ejemplo — Worker Threads (Node.js)

```js
const { Worker, isMainThread, parentPort, workerData } = require('node:worker_threads');

if (isMainThread) {
  // Este código se ejecuta en el hilo principal
  const worker = new Worker(new URL(import.meta.url), {
    workerData: { num: 5 }
  });

  worker.on('message', (result) => {
    console.log('Recibido del worker:', result);
  });
} else {
  // Este código se ejecuta en el hilo del worker
  const { num } = workerData;
  const result = num * 2;
  parentPort.postMessage(result);
}
```

Este ejemplo demuestra: cómo detectar si el código corre en el hilo principal (`isMainThread`), cómo pasar datos iniciales (`workerData`), y cómo el worker responde al hilo principal (`parentPort.postMessage`).

### Ejemplo — Mecánica de Web Worker (envío de mensaje desde el hilo principal)

```js
// en main.js
const myWorker = new Worker('heavy-task-worker.js');
const dataToSend = { command: 'processData', payload: [1, 2, 3, 4, 5] };
myWorker.postMessage(dataToSend);
```

### Ejemplo — Recepción en el Worker (Principal a Worker)

```js
// en heavy-task-worker.js
self.onmessage = function(event) {
  // Los datos enviados desde el hilo principal están en event.data
  const receivedData = event.data;
  console.log('Worker recibió:', receivedData);

  if (receivedData.command === 'processData') {
    const result = receivedData.payload.reduce((sum, val) => sum + val, 0);

    self.postMessage({ status: 'complete', result: result });
  }
};
```

Este ejemplo demuestra el procesamiento de un payload numérico (suma con `reduce`) dentro del worker y el envío del resultado de vuelta.

### Ejemplo — Recepción en el Principal (Worker a Principal)

```js
// en main.js
myWorker.onmessage = function(event) {
  const workerResult = event.data;
  console.log('Script principal recibió:', workerResult);

  if (workerResult.status === 'complete') {
    document.getElementById('result-display').innerText = workerResult.result;
  }
};
```

### Ejemplo — Objetos Transferibles

```js
// En el hilo principal
const myWorker = new Worker('worker.js');
// Enviar el ArrayBuffer al worker, transfiriendo la propiedad
myWorker.postMessage(myBuffer, [myBuffer]);


// En el WebWorker
self.onmessage = (event) => {
  const receivedBuffer = event.data;

  doSomeOperation(receivedBuffer);

  // Transferir el ArrayBuffer modificado de vuelta al hilo principal
  self.postMessage(receivedBuffer, [receivedBuffer]);
};
```

Este ejemplo demuestra cómo transferir la propiedad de un `ArrayBuffer` (en vez de clonarlo) tanto al enviarlo al worker como al devolverlo modificado al hilo principal, usando el segundo argumento de `postMessage` (lista de objetos transferibles).

### Ejemplos de código para tipos de Web Workers

- Worker Dedicado: `const worker = new Worker('your-worker.js');`
- Worker Compartido: `const sharedWorker = new SharedWorker('shared-worker.js');`

## Comparaciones

### Diferencias — Módulo Cluster vs Worker Threads

| Criterio | Módulo Cluster | Worker Threads |
|---|---|---|
| Modelo de Paralelismo | Vía Multi-proceso | Vía Multi-hilo |
| Memoria | Memoria Aislada (no compartida) | Memoria Compartida (ej. SharedArrayBuffer) |
| Mejor Para | Tareas ligadas a I/O (como servidores web) | Tareas intensivas de CPU (ej. cálculos) |
| Comunicación | Más lenta, necesita Comunicación Entre Procesos | Más rápida, usa Memoria compartida |
| Costo de Recursos | Mayor (cada proceso es más pesado) | Menor (los hilos son ligeros) |

Frase de cierre del documento sobre esta comparación: **"Un solo hilo para I/O, multi-hilo cuando sea necesario para trabajo de CPU"**.

### Comparación implícita — Multiprocesamiento vs Multihilo (repaso general)

| Aspecto | Multiprocesamiento | Multihilo |
|---|---|---|
| Aislamiento | Cada proceso está completamente aislado (memoria, recursos, entorno propios) | Los hilos comparten memoria, manejadores de archivos y recursos del sistema |
| Comunicación | Requiere mecanismos explícitos (tuberías, colas, memoria compartida, sockets) | Directa (mismo espacio de memoria), pero requiere sincronización |
| Sobrecarga (overhead) | Mayor sobrecarga en creación y cambio de contexto | Sobrecarga mínima en creación y cambio de contexto |
| Riesgo principal | Aislamiento evita corrupción de datos entre procesos | Riesgo de condiciones de carrera y corrupción de datos sin sincronización |

## Información visual relevante

### Diagrama "Multiprocesamiento"

Muestra dos cajas independientes (dos procesos), cada una con los bloques Code, Data, Files, Registers, Counter, Stack, y una flecha desde un bloque "Thread" hacia el Counter. Representa que cada proceso tiene su propia copia completa de recursos y al menos un hilo propio, ilustrando el aislamiento entre procesos.

### Diagrama "Multihilo de Procesador Único"

Muestra una sola caja (un proceso) dividida en tres columnas, cada una representando un hilo (Thread) con sus propios Registers, Counter y Stack, pero compartiendo los bloques superiores Code, Data y Files. Ilustra que los hilos comparten memoria/recursos del proceso pero mantienen su propia pila de ejecución.

### Diagrama "Event Loop" (Node de un solo hilo)

Círculo central etiquetado "Event Loop / Single thread". Flechas de entrada desde "Requests, Async Operations, etc." hacia el loop. Desde el loop sale una flecha "register callback" hacia una "Queue" que contiene tres tipos de operaciones intensivas: File System, Computation, Heavy task (etiquetadas como "Intensive Operations"). Cuando una operación se completa, una flecha "operation complete" retorna hacia el loop, y otra flecha "Trigger Callback" sale del loop de vuelta al flujo de entrada. Ilustra el modelo no bloqueante de E/S asíncrona de Node.js.

### Diagrama "Módulo Cluster"

Nodo superior "Master Process" con tres flechas hacia nodos inferiores: "Child Process 1", "Child Process 1" (nombre repetido, posible error tipográfico en la fuente — debería probablemente decir "Child Process 2") y "Child Process N". Ilustra la arquitectura maestro-trabajador del módulo Cluster.

### Diagrama "Worker Threads — Arquitectura"

Nodo superior "Main Event Loop" con dos flechas etiquetadas `new Worker()` hacia dos cajas verdes idénticas, cada una mostrando íconos de Node.js, V8 y libuv, junto con la etiqueta "1 Event Loop / 1 Thread". Ilustra que cada worker thread tiene su propia instancia completa de V8, su propio bucle de eventos y su propio hilo, todo dentro del mismo proceso Node.

### Diagrama "Web Workers"

Muestra un ícono de "Web Page" (ventana de navegador con elementos de color) conectado mediante una flecha bidireccional a un ícono de engranaje etiquetado "Web Worker", el cual a su vez está asociado a una etiqueta "Heavy Computations". Ilustra la comunicación bidireccional entre la página principal y el worker para delegar cómputo pesado.

## Terminología importante

- **Proceso**: Instancia completa y aislada de ejecución, con su propio espacio de memoria, recursos y entorno de ejecución.
- **Hilo (Thread)**: Secuencia independiente de instrucciones dentro de un proceso; comparte memoria y recursos con otros hilos del mismo proceso.
- **Condición de carrera (Race condition)**: Riesgo que surge cuando múltiples hilos acceden a memoria compartida sin sincronización adecuada, pudiendo causar corrupción de datos.
- **Event Loop (Bucle de eventos)**: Modelo de ejecución de un solo hilo en Node.js que maneja operaciones concurrentes mediante E/S asíncrona y no bloqueante.
- **Módulo Cluster**: Característica de Node.js para crear procesos hijos (workers) y aprovechar sistemas multinúcleo, mejorando rendimiento y confiabilidad.
- **cluster.fork()**: Método del módulo Cluster para generar procesos trabajadores.
- **Round-robin simple**: Estrategia de balanceo de carga por defecto del módulo Cluster; el maestro distribuye cada nueva conexión al siguiente trabajador en secuencia.
- **Worker Threads**: API de Node.js que permite ejecución paralela de código JavaScript en hilos separados dentro del mismo proceso.
- **SharedArrayBuffer**: Mecanismo que permite a los worker threads de Node.js compartir memoria entre sí, aunque cada uno tiene su propia instancia de V8, bucle de eventos y heap.
- **workerData**: Datos iniciales pasados a un Worker Thread de Node.js al crearlo.
- **parentPort**: Objeto usado dentro de un Worker Thread de Node.js para comunicarse (postMessage) con el hilo principal.
- **Web Workers**: Característica del navegador que permite ejecutar código JavaScript en hilos en segundo plano, separados del hilo principal de la UI, logrando paralelismo verdadero.
- **self**: Ámbito global dentro de un Web Worker, usado para escuchar el evento `onmessage` y enviar mensajes con `self.postMessage()`.
- **postMessage()**: Método usado para enviar datos entre el hilo principal y un worker (Node.js o navegador); en el contexto de Web Workers, los datos enviados son clonados, no compartidos, salvo que se usen objetos transferibles.
- **Objetos Transferibles**: Mecanismo para datos grandes que transfiere la propiedad de los datos (por ejemplo, un ArrayBuffer) en lugar de clonarlos, mejorando el rendimiento.
- **Workers Dedicados**: Tipo de Web Worker más común; se ejecuta en un hilo de fondo dedicado con alcance aislado, no se puede compartir entre scripts; usado para cálculos pesados (procesamiento de imágenes, análisis de datos).
- **Workers Compartidos (SharedWorker)**: Tipo de Web Worker accesible por múltiples scripts, incluso desde diferentes ventanas/pestañas del mismo origen; requieren comunicación basada en puertos; persisten mientras la página conectada esté abierta; usados para compartir estado entre múltiples pestañas.
- **Service Workers**: Tipo de Web Worker diseñado para funcionalidad offline e intercepción de peticiones de red; actúa como proxy de red; tiene ciclo de vida independiente de las páginas web; soporta notificaciones push y sincronización en segundo plano.

## Notas y advertencias

- El texto del repaso de multihilo contiene errores tipográficos evidentes en la fuente: "proceso finico" (probablemente "proceso único") y "mfiltiples hilos" (probablemente "múltiples hilos"). Se preservan como aparecen en el documento.
- El diagrama del Módulo Cluster muestra dos nodos etiquetados idénticamente como "Child Process 1" (uno de ellos probablemente debería decir "Child Process 2"), lo cual parece un error de la fuente; se señala aquí sin corregirlo.
- El documento no define explícitamente el término "condición de carrera" (race condition); solo lo menciona como riesgo a prevenir mediante sincronización, sin explicar el concepto en profundidad.
- No se desarrolla en detalle el mecanismo interno de "sincronización" entre hilos (no se mencionan primitivas específicas como mutex, semáforos, locks, etc.).
- El ejemplo de Worker Threads usa `new URL(import.meta.url)` para referenciar el propio archivo como script del worker, pero el documento no explica explícitamente esta técnica (ejecutar el mismo archivo tanto como hilo principal como worker mediante la condición `isMainThread`); se infiere del código pero no se detalla en el texto.
- El módulo Cluster se documenta con `cluster.isMaster`; nota: en versiones recientes de Node.js esta propiedad fue renombrada a `cluster.isPrimary`, pero el documento usa la forma `isMaster`, la cual se preserva tal como aparece en la fuente sin corrección.
- El diagrama de Worker Threads menciona íconos de "Node.js, V8, libuv" pero no explica en el texto qué es libuv ni su rol específico; se preserva solo como elemento visual identificado.
- La sección de "Service Workers" es breve (una lista de características) sin ejemplos de código, a diferencia de Workers Dedicados y Compartidos que sí incluyen fragmentos de código.
- No se incluye contenido explícito de ejercicios prácticos, evaluación o tareas para la semana; el documento es puramente expositivo/teórico con ejemplos de código.

## Fuente

- Documento: week-7.pdf (Programación 7 — Semana 7: Programación Paralela II, Jala University, material 2026)
- Secciones o páginas relevantes: páginas 1 (portada), 2 (sección "Multiprocesamiento y Multihilo"), 3–7 (repaso de Multiprocesamiento y Multihilo), 8–20 (sección "Procesamiento Paralelo — Contexto de Node": Node de un solo hilo, Event Loop, Módulo Cluster, contexto histórico, Worker Threads), 21–24 (continuación de Worker Threads y contexto histórico, tabla comparativa Cluster vs Worker Threads), 25–36 (sección "Procesamiento Paralelo — Contexto Web": Web Workers, mecánica, consideraciones, objetos transferibles, tipos de Web Workers, cierre).
