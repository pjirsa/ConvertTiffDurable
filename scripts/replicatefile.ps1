$item = Get-Item -Path ".\sample.tiff"
for ($var = 1; $var -lt 10; $var++) {
    $id = New-Guid
    $item.CopyTo($PWD.Path + "\" + $id + ".tiff", $true)
}