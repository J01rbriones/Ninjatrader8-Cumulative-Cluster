# NT8-Cumulative-Cluster

![Vista General del Indicador](salida_cluster.png)

## 🎯 ¿Qué hace este indicador?
Este indicador avanzado de **Order Flow** para NinjaTrader 8 rastrea la agresividad institucional en tiempo real. Su diferencial es que no solo mide el volumen nominal, sino que calcula el **valor monetario real ($)** de las órdenes, permitiendo identificar dónde se está inyectando capital pesado.

## 📊 Monitoreo de Datos y Salidas (Output)
El script está diseñado para enviar información detallada a dos pestañas distintas en la ventana de **Salida de NinjaScript**, facilitando la lectura sin saturar al trader:

### 1. Registro de Clústeres Confirmados (Salida Pestaña 2)
Esta pestaña muestra exclusivamente los clústeres que han sido dibujados en el gráfico. 
* **Filtro inteligente:** Solo imprime información si el valor monetario del clúster supera el **Output Threshold ($)** configurado en los parámetros.
* **Uso:** Ideal para auditar los puntos de interés que ves en el gráfico.

### 2. Flujo Detallado de Órdenes (Salida Pestaña 1)
Aquí se imprime el flujo constante de órdenes agresivas que golpean el Bid y el Ask.
* **Sin filtros:** A diferencia de los clústeres, esta pestaña registra todas las órdenes detectadas para un seguimiento exhaustivo del tape.
* **Detalle:** Incluye precio exacto, instrumento y valor total de la transacción.

![Detalle](salida_ordenes.png)

## ✨ Funciones clave:
* **Cálculo de Valor Real:** Soporta multiplicadores automáticos para activos principales (**ES, NQ, MES, MNQ, YM**, etc.).
* **Clústeres Dinámicos:** Dibuja elipses cuya opacidad y tamaño reaccionan proporcionalmente a la intensidad del capital.
* **Totales Globales:** Muestra acumuladores en tiempo real (Local y Global) directamente en las esquinas del gráfico para medir el sentimiento de la sesión.

## 🛠 Parámetros Técnicos
| Parámetro | Descripción |
| :--- | :--- |
| **Cluster Threshold** | Volumen mínimo necesario para que se genere un clúster visual. |
| **Output Threshold ($)** | Valor monetario mínimo para que un clúster se registre en la Pestaña 2 de salida. |
| **Normalization Multiplier** | Sensibilidad de la escala visual (ajusta el tamaño de los círculos). |
| **Cluster Circle Max Scale** | Tamaño máximo permitido para las elipses en el gráfico. |

## 🚀 Instalación
1. Descarga el archivo `CumulativeCluster.cs` de este repositorio.
2. Colócalo en la ruta: `Documentos/NinjaTrader 8/bin/Custom/Indicators`.
3. Abre NinjaTrader 8, ve al **NinjaScript Editor** y presiona **F5** para compilar.
4. El indicador aparecerá como `Cumulative Cluster` en tu lista habitual.

---
## ⚖️ Descargo de Responsabilidad y Gestión de Riesgos

**Advertencia de Riesgo:** El comercio de futuros y derivados conlleva un riesgo sustancial de pérdida y no es adecuado para todos los inversores. El rendimiento pasado no es necesariamente indicativo de resultados futuros. El uso de este indicador es bajo su propia responsabilidad y el autor no se hace responsable de las pérdidas financieras derivadas del uso de este software.

**Optimización Estratégica:** Para maximizar la eficacia de esta herramienta, se recomienda integrar el análisis del flujo de órdenes con el estudio de las **griegas de opciones sobre índices**. Comprender el posicionamiento del mercado de opciones permite mejorar significativamente la lectura de la rentabilidad direccional en contratos de futuros, proporcionando un contexto institucional superior al análisis técnico convencional.
