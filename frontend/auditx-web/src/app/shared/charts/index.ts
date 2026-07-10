/**
 * Hand-rolled, dependency-free inline-SVG chart components. Each is standalone,
 * OnPush, signal-based, responsive (fixed viewBox + `width:100%`), accessible
 * (`role="img"` + `<title>`), and themed via CSS variables. Colours resolve from
 * the shared semantic palette in `chart-colors.ts`.
 */
export { BarChartComponent } from './bar-chart/bar-chart.component';
export { DonutChartComponent } from './donut-chart/donut-chart.component';
export { GanttChartComponent } from './gantt-chart/gantt-chart.component';
export { GaugeChartComponent } from './gauge-chart/gauge-chart.component';
export { LineChartComponent } from './line-chart/line-chart.component';
export type { ChartDatum, GanttItem, PointDatum } from './chart-types';
export {
  resolveColor,
  semanticColor,
  categoricalColor,
} from './chart-colors';
