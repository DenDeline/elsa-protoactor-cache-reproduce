{{- define "elsa-protoactor-repro.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "elsa-protoactor-repro.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{- define "elsa-protoactor-repro.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "elsa-protoactor-repro.labels" -}}
helm.sh/chart: {{ include "elsa-protoactor-repro.chart" . }}
{{ include "elsa-protoactor-repro.selectorLabels" . }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{- define "elsa-protoactor-repro.selectorLabels" -}}
app.kubernetes.io/name: {{ include "elsa-protoactor-repro.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{- define "elsa-protoactor-repro.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "elsa-protoactor-repro.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{- define "elsa-protoactor-repro.clusterName" -}}
{{- default (include "elsa-protoactor-repro.fullname" .) .Values.protoActor.clusterName | trunc 63 | trimSuffix "-" }}
{{- end }}
