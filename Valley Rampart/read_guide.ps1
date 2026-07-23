$src = 'd:\Valley Rampart\开发引导书\主菜单初始化引导.md'
$dst = 'd:\Valley Rampart\Valley Rampart\guide_temp.md'
Copy-Item -Path $src -Destination $dst -Force
Write-Output "Done"