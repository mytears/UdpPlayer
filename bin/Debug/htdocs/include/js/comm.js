// 널체크 
function getChkNull(p_src, p_default) {
    if (p_src == null || p_src == undefined) {
        return p_default;
    } else {
        return p_src + "";
    }
}

function getCvtRemoveWhite(p_src) {
    var p1 = / /gi;
    var p2 = /\t/gi;
    var p3 = /\n/gi;
    var p4 = /\r/gi;

    if (p_src == null || p_src == undefined) {
        return "";
    }

    p_src = p_src + "";
    p_src = p_src.replace(p1, "");
    p_src = p_src.replace(p2, "");
    p_src = p_src.replace(p3, "");
    p_src = p_src.replace(p4, "");
    p_src = p_src.trim();
    return p_src;
}

function getUrlParams(p_url) {
    var params = {};
    p_url.replace(/[?&]+([^=&]+)=([^&]*)/gi, function (str, key, value) {
        params[key] = value;
    });
    return params;
}

function getPosTransform(p_obj) {
    var i = 0;
    var ret_obj = {
        left: 0,
        top: 0,
        scale: 1,
        rotate: 0
    };
    var str_tmp = "";
    var arr_tmp = [];
    var arr_match = [];

    var str_trans = p_obj.style.transform;
    var regex = /(\w+)\((.+?)\)/g;
    var p1 = /px/gi;
    var p2 = /deg/gi;

    while (arr_match = regex.exec(str_trans)) {
        if (arr_match.length == 3) {
            if (arr_match[1] == "translate") {
                arr_tmp = arr_match[2].split(',');
                if (arr_tmp.length == 2) {
                    str_tmp = arr_tmp[0].replace(p1, "");
                    ret_obj.left = parseFloat(str_tmp);
                    str_tmp = arr_tmp[1].replace(p1, "");
                    ret_obj.top = parseFloat(str_tmp);
                }
            }
        }
        i++;
        if (i >= 10) break;
    }

    return ret_obj;
}



////////////////////////////////////////////////////
// 연동


function setCallWebToApp(p_cmd, p_val) {
    var str_cmd = "";
    console.log("setCallWebToApp = " + p_cmd + " , " + p_val);
    if (window.chrome.webview) {
        str_cmd = p_cmd + " ${" + p_val + "}";
        window.chrome.webview.postMessage(str_cmd);
    }
}

function setCallWebToAppSock(p_cmd, p_val) {
    var str_cmd = "";

    //console.log("setCallWebToAppSock = " + p_cmd + " , " + p_val);

    str_cmd = p_cmd + "^" + p_val;

    var str_url = "ws://127.0.0.1:8008/echo";

    var web_sock = new WebSocket(str_url);

    web_sock.onopen = function (evt) {
        web_sock.send(str_cmd);
        web_sock.close();
    }
}


// GET 방식처리
function setSendSocketGET(p_fnc, p_url, p_opt) {
    var xhr;
    var fnc;
    if (window.XMLHttpRequest) {
        xhr = new XMLHttpRequest();
    } else {
        xhr = new ActiveXObject("Microsoft.XMLHTTP");
    }

    if (p_opt.timeout > 0) {
        xhr.timeout = p_opt.timeout;
    }

    fnc = p_fnc;

    xhr.onreadystatechange = function () {
        if (xhr.readyState != 4) {
            return;
        }
        // 성공을 했다.
        if (xhr.status == 200) {
            //console.log("setSendSocketGET = " + xhr.status);
            // 받은 정보를 콜백하자.
            str_ret = xhr.responseText;
            str_ret = $.trim(str_ret);
            str_ret = str_ret.replace(/\n/g, "");
            fnc("SUCC", str_ret);
        } else { // 실패를 했다.
            fnc("FAIL", "");
        }
    }

    xhr.open("GET", p_url, true);
    xhr.send();
}

// POST 방식처리
function setSendSocketPOST(p_fnc, p_url, p_opt, p_form, p_body) {
    var str_ret = "";
    var xhr;
    var fnc;
    if (window.XMLHttpRequest) {
        xhr = new XMLHttpRequest();
    } else {
        xhr = new ActiveXObject("Microsoft.XMLHTTP");
    }

    if (p_opt.timeout > 0) {
        xhr.timeout = p_opt.timeout;
    }

    fnc = p_fnc;

    xhr.onreadystatechange = function () {
        if (xhr.readyState != 4) {
            return;
        }
        // 성공을 했다.
        if (xhr.status == 200) {
            // 받은 정보를 콜백하자.
            str_ret = xhr.responseText;
            str_ret = $.trim(str_ret);
            str_ret = str_ret.replace(/\n/g, "");
            fnc("SUCC", str_ret);
        } else { // 실패를 했다.
            fnc("FAIL", "");
        }
    }

    xhr.open("POST", p_url, true);
    if (p_opt.type == "JSON") {
        xhr.setRequestHeader("Content-Type", "application/json");
        xhr.send(p_body);
    } else {
        xhr.send(p_form);
    }
}

// 결과를 받는다.
function onReadSockContents() {
    var str_ret = "";
    //console.log("gl_http = " + gl_http.status);
    if (gl_http.readyState != 4) {
        return;
    }
    // 성공을 했다.
    if (gl_http.status == 200) {
        //console.log("onReadSockContents = " + gl_http.status);
        // 받은 정보를 콜백하자.
        str_ret = gl_http.responseText;
        str_ret = $.trim(str_ret);
        str_ret = str_ret.replace(/\n/g, "");
        gl_fnc("SUCC", str_ret);
        //  gl_http.responseText;
    } else { // 실패를 했다.
        gl_fnc("FAIL", "");
    }
}

//이미지 주소를 보정
function convFilePath(_path) {
    let t_path = "";
    if (m_notice_mode == "web") {
        t_path = m_root_url + _path.replace(/^commonfiles\//, '');
    } else {
        t_path = _path;
    }

    return t_path;
}
