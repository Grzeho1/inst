# Cesta k repo (automaticky aktuÃ¡lnÃ­ sloÅ¾ka)
$repoPath = (Get-Location).Path
cd $repoPath

$sqlFolder = "$repoPath\sql"

# StaÅ¾enÃ­ aktuÃ¡lnÃ­ch zmÄ›n z GitHubu
git pull origin main

# PÅ™idÃ¡nÃ­ pouze sloÅ¾ky sql
git add sql/

$changes = git status --porcelain
if ($changes) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    git commit -m "Auto commit SQL zmÄ›n - $timestamp"
    git push origin main
    Write-Host "Changes --> GitHub"
} else {
    Write-Host "nothing for update"
}
Write-Host ""
Write-Host "Stiskni libovolnou klávesu pro zavření okna..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
