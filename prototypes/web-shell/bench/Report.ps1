param([string]$Evidence="$PSScriptRoot/../../../artifacts/web-lab/evidence")
$ErrorActionPreference='Stop'
function Total($sample,$property){($sample.rows | Measure-Object -Property $property -Sum).Sum}
function N($n,$digits=1){$n.ToString("F$digits",[cultureinfo]'es-ES')}
function Cpu($s,$t){
 $seconds=0.0
 foreach($row in $t.rows){$initial=$s.rows|where id -eq $row.id
  $seconds+=if($initial){$row.cpu-$initial.cpu}else{$row.cpu}
 }
 N (100*$seconds/($t.seconds-$s.seconds))
}
'| Host y escena | Lista React inicial / todas (s) | Procesos | Privada / WS (MiB) | Handles | GDI / USER | Composition / Event | CPU en reposo (% de un núcleo) |'
'|---|---:|---:|---:|---:|---:|---:|---:|'
foreach($scene in 'one','three','exercise'){
 foreach($hostType in 'A','B'){
  $run=Get-Content "$Evidence/$hostType-$scene.json" -Raw|ConvertFrom-Json
  $idle=$run.samples|where stage -eq 'idle-end'
  $sample=if($scene -eq 'exercise'){$run.samples|where stage -eq 'popover'}else{$idle}
  $name=@{one='1 contenedor';three='3 contenedores';exercise='3 + dock + popover'}[$scene]
  "| $hostType · $name | $(N $run.firstReactSeconds 2) / $(N $run.allReadySeconds 2) | $($sample.rows.Count) | $(N ((Total $sample private)/1MB)) / $(N ((Total $sample ws)/1MB)) | $(Total $sample handles) | $(Total $sample gdi) / $(Total $sample user) | $(Total $sample composition) / $(Total $sample events) | $(Cpu $run.samples[0] $idle) |"
 }
}
''
'| Tras ocho solicitudes de reapertura | Cargas de popover confirmadas (incluye la inicial) | CPU de interacción (% de un núcleo) | Privada inicial → final (MiB) | Handles inicial → final | Composition / Event inicial → final | Procesos vivos a 3 / 10 s del cierre |'
'|---|---:|---:|---:|---:|---:|---:|'
foreach($hostType in 'A','B'){
 $run=Get-Content "$Evidence/$hostType-exercise.json" -Raw|ConvertFrom-Json
 $initial=$run.samples|where stage -eq 'idle-end'
 $final=$run.samples[-1]
 $during=$run.samples|where stage -eq 'cycle-start'
 $end=$run.samples|where stage -eq 'cycle-end'
 "| $hostType | $($run.popupLoads) | $(Cpu $during $end) | $(N ((Total $initial private)/1MB)) → $(N ((Total $final private)/1MB)) | $(Total $initial handles) → $(Total $final handles) | $(Total $initial composition) / $(Total $initial events) → $(Total $final composition) / $(Total $final events) | $($run.afterThree.Count) / $($run.survivors.Count) |"
}
