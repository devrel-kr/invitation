Param(
    [Parameter(Mandatory = $true)]
    [string] $InputFile,

    [Parameter(Mandatory = $true)]
    [string] $OutputFile,

    [Parameter(Mandatory = $true)]
    [string] $DueDate,

    [Parameter(Mandatory = $true)]
    [string] $Organization
)

Import-Module "$PSScriptRoot/InvitationRequest.psm1" -Force

$issue = Read-IssuePayload -Path $InputFile

$requestType = Get-IssueFormValue -Body $issue.body -Label '요청 유형'
$organisation = Get-IssueFormValue -Body $issue.body -Label '조직'
$profileUrl = Get-IssueFormValue -Body $issue.body -Label 'GitHub 프로필 링크'
$name = Get-IssueFormValue -Body $issue.body -Label '이름'
$email = Get-IssueFormValue -Body $issue.body -Label '이메일'

if ($organisation) { $organisation = $organisation.TrimEnd('/') }
if ($profileUrl) { $profileUrl = $profileUrl.TrimEnd('/') }

$invalidReasons = [System.Collections.Generic.List[string]]::new()

if ($requestType -ne 'Azure 구독 초대 요청') {
    $invalidReasons.Add('요청 유형이 올바르지 않습니다.')
}

if (-not $organisation -or $organisation -ne $Organization) {
    $invalidReasons.Add('Azure 조직 URL이 올바르지 않습니다.')
}

if ((Test-GitHubProfileUrl -Url $profileUrl) -eq $false) {
    $invalidReasons.Add('GitHub 프로필 URL이 올바르지 않습니다.')
}

if (-not $name) {
    $invalidReasons.Add('이름이 올바르지 않습니다.')
}

if ((Test-AllowedEmail -Email $email) -eq $false) {
    $invalidReasons.Add('이메일 주소가 올바르지 않습니다.')
}

$githubHandle = if ($profileUrl) { $profileUrl -replace '^https://github\.com/', '' } else { $null }

Write-InvitationResult `
    -Issue $issue `
    -DueDate $DueDate `
    -OutputFile $OutputFile `
    -InvalidReasons $invalidReasons `
    -AuthorMismatchReason 'GitHub 프로필 URL이 이슈 작성자와 일치하지 않습니다.' `
    -Body @{
        requestType = $requestType
        organisation = $organisation
        githubHandle = $githubHandle
        name = $name
        email = $email
    }
