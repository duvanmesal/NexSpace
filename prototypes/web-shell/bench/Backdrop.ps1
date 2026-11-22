# A controlled window behind the two native shells. Reads the Windows stock wallpaper.
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$form=New-Object Windows.Forms.Form
$form.Text='NexSpace · Fondo de comparación'
$form.FormBorderStyle='None'
$form.StartPosition='Manual'
$form.Bounds=[Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$form.BackColor=[Drawing.Color]::FromArgb(36,38,42)
$path=Join-Path $env:WINDIR 'Web/Wallpaper/Windows/img19.jpg'
if(Test-Path $path){$picture=[Drawing.Image]::FromFile($path);$form.BackgroundImage=$picture;$form.BackgroundImageLayout='Stretch'}
try{[void]$form.ShowDialog()}finally{$form.Dispose();if($picture){$picture.Dispose()}}
