
$repoPath = (Get-Location).Path
$sshFolder = "$env:USERPROFILE\.ssh"
$privateKey = Join-Path $sshFolder "id_ed25519"
$publicKey = "$privateKey.pub"
$remoteUrl = "git@github.com:Grzeho1/sql.git"
if (-not (Test-Path $sshFolder)) {
    New-Item -ItemType Directory -Path $sshFolder -Force
}

git remote set-url origin $remoteUrl
Write-Host "[INFO] Remote origin přepsán na: $remoteUrl"
$changes = $null
$timestamp = $null


Set-Location $repoPath


if (-not (Test-Path $privateKey)) {
    Write-Host "[INFO] SSH klíč nebyl nalezen – generuji..." -ForegroundColor Yellow
    ssh-keygen -t ed25519 -C "$env:USERNAME@$(hostname)" -f $privateKey -q

    if (Test-Path $publicKey) {
        $pubKey = Get-Content $publicKey
        Set-Clipboard -Value $pubKey
        Write-Host "`n[INFO] Veřejný klíč byl zkopírován do schránky:" -ForegroundColor Green
        Write-Host $pubKey -ForegroundColor Cyan
   
        Read-Host "`nPo přidání klíče na GitHub stiskni Enter pro pokračování..."
    } else {
        Write-Host "[ERROR] Klíč nebyl úspěšně vytvořen." -ForegroundColor Red
        Read-Host "Stiskni Enter pro pokračování..."
        exit 1
    }
}


if (-not (Get-Process -Name "ssh-agent" -ErrorAction SilentlyContinue)) {
    Start-Process -NoNewWindow -FilePath "ssh-agent" -ArgumentList "-s"
}
ssh-add $privateKey


if (-not (Test-Path "$repoPath\.git")) {
    Write-Host "[INFO] Tato složka není git repozitář – inicializuji..." -ForegroundColor Yellow
    git init
    git remote add origin $remoteUrl
    Write-Host "[INFO] Přidán remote origin: $remoteUrl"
    git checkout -b main
    if (-not (git log -1 2>$null)) {
        Write-Host "[INFO] Vytvářím počáteční commit..." -ForegroundColor Yellow
        git commit --allow-empty -m "Initial commit"
    }
}


$remoteUrl = git remote get-url origin 2>$null
if ($remoteUrl -like "https://github.com/*") {
    $sshUrl = $remoteUrl -replace "https://github.com/", "git@github.com:"
    git remote set-url origin $sshUrl
    Write-Host "[INFO] Remote přepnut na SSH: $sshUrl"
}


if (-not (Test-Path "db-update")) {
    Write-Host "[ERROR] Složka db-update neexistuje." -ForegroundColor Red
    Read-Host "Stiskni Enter pro pokračování..."
    exit 1
}


git add -A db-update/sql/ 2>$null
git add -A db-update/Univerzal_SQL/
git add -A db-update/Shoptet_SQL/
$changes = git status --porcelain

if ($changes) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    git commit -m "Auto commit SQL změn - $timestamp"
    $pushResult = git push origin main --force 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] Změny byly odeslány na GitHub (force push)." -ForegroundColor Green
} else {
    Write-Host "[CHYBA] Push selhal:" -ForegroundColor Red
    Write-Host $pushResult
}

} else {
    Write-Host "[INFO] Žádné změny k odeslání." -ForegroundColor Gray
}

Write-Host ""

Read-Host "Stiskni Enter pro zavření okna"