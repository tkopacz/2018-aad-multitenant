(function () {

    var config = {
        instance: 'https://login.microsoftonline.com/',
        tenant: 'tkdxpl.onmicrosoft.com',
        clientId: '580bbb12-7061-4bcb-aaff-e28b847ec9f6',
        postLogoutRedirectUri: window.location.origin,
        cacheLocation: 'localStorage', // enable this for IE, as sessionStorage does not work for localhost.
    }

    var authContext = new AuthenticationContext(config);

    authContext.handleWindowCallback();
    $(".app-error").html(authContext.getLoginError());

    var isCallback = authContext.isCallback(window.location.hash);
    if (isCallback && !authContext.getLoginError()) {
        window.location = authContext._getItem(authContext.CONSTANTS.STORAGE.LOGIN_REQUEST);
    }

    $(".app-Login").click(function () {
        authContext.config.redirectUri = window.location.href;
        authContext.login();
    });
    $(".app-ShowUser").click(function () {
        var user = authContext.getCachedUser();
        alert("user: " + JSON.stringify(user));
    });
    
    $(".app-CallAPI").click(function () {
        authContext.acquireToken(authContext.config.clientId, function (error, token) {
            // Handle ADAL Error
            if (error || !token) {
                alert('ADAL Error Occurred: ' + error);
                return;
            }
            $.ajax({
                type: "GET",
                url: "/api/values",
                headers: {
                    'Authorization': 'Bearer ' + token
                },
            }).done(function (data) {
                    alert("data: " + JSON.stringify(data));
                }).fail(function (err) {
                    alert("error");
                }).always(function () {
                });
        });

    });

}());
