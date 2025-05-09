let m_status_time_chk = 0;
let m_time_last = 0;
let m_contents_url = "";
let m_root_url = "";
let m_notice_mode = "";
let m_header = null;
let m_contents_list = [[], [], [], []];

let m_curr_notice = 1;
let m_curr_notice_ptime = 0;
let m_curr_notice_type = "";
let m_curr_notice_cnt = -1;
let m_notice_timeout = null;
let m_curr_admin = 1;

let m_curr_page = "";

function setInit() {
    
    $(".send_btn").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickUdpSendBtn(this);
    });

    $(".next_btn").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickNextBtn(this);
    });

    $(".home_btn").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickHomeBtn(this);
    });

    $(".close_btn").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickCloseBtn(this);
    });

    $(".pos_btn").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickPopupBtn(this);
    });

    $("html").on("touchstart mousedown", function (e) {
        e.preventDefault();
        setTouched();
    });

    $("video").on("play pause ended seeked seeking volumechange ratechange loadeddata canplay canplaythrough waiting stalled error", function (event) {
        //console.log(`이벤트 발생: ${event.type}`, this);
    });

    m_time_last = new Date().getTime();
    setInterval(setMainInterval, 1000);
    setLoadSetting("include/setting.json");
    setInitFsCommand();
}

function setLoadSetting(_url) {
    $.ajax({
        url: _url,
        dataType: 'json',
        success: function (data) {
            m_contents_url = data.setting.content_url;
            m_root_url = data.setting.root_url;
            m_notice_mode = data.notice_mode;
            setContents();
        },
        error: function (xhr, status, error) {
            console.error('컨텐츠 에러 발생:', status, error);
        },
    });
}

//메인 타이머
function setMainInterval() {
    var time_gap = 0;
    var time_curr = new Date().getTime();

    time_gap = time_curr - m_time_last;
    time_gap = Math.floor(time_gap / 1000);
    if (time_gap > 180) {
        if ($(".page_00").css("display") == "none") {
            setMainReset();
        }
    }

    m_status_time_chk += 1;
    if (m_status_time_chk > 60) {
        m_status_time_chk = 0;
        setCallWebToApp('STATUS', 'STATUS');
    }
}

function setTouched() {
    m_time_last = new Date().getTime();
}


//kiosk_contents를 읽기
function setContents() {
    var t_url = m_contents_url;
    $.ajax({
        url: t_url,
        dataType: 'json',
        success: function (data) {
            m_header = data.header;
            setInitSetting();
        },
        error: function (xhr, status, error) {
            console.error('컨텐츠 에러 발생:', status, error);
            setInitSetting();
        },
    });
}

//로딩 커버 가리기
function setHideCover() {
    if ($(".cover").css("display") != "none") {
        $('.cover').hide();
    }
}

//초기화
function setInitSetting() {
    setTimeout(setHideCover, 500);
    setPage("00");
}

function setMainReset() {
    m_clickable = true;
    $(".popup_page").hide();
    setPage("00");
}

function setShowPopup(_cate, _num) {
    console.log("setShowPopup", _cate, _num);
    $(".popup_page").show();
}

function setHidePopup() {
    m_clickable = true;
    $(".popup_page").fadeOut();
}

function convStr(_str) {
    if (_str == null || _str == "null") {
        return "";
    } else {
        return _str.replace(/(\r\n|\n\r|\n|\r)/g, '<br>');
    }
}

function onClickUdpSendBtn(_obj) {
    setCallWebToApp('UDP_SEND', 'MSG_UDP_SEND');
}

function onClickPrevBtn(_obj) {
}

function onClickNextBtn(_obj) {
}

function setPrevNextBtnState(t_sub, t_max) {
    //console.log("setPrevNextBtnState", t_sub, t_max);
    if (t_sub == 0) {
        $(".prev_btn").addClass("disabled");
    } else {
        $(".prev_btn").removeClass("disabled");
    }

    if (t_sub == t_max - 1) {
        $(".next_btn").addClass("disabled");
    } else {
        $(".next_btn").removeClass("disabled");
    }
}

function onClickHomeBtn(_obj) {
    setPage("10");
}

function onClickPopupBtn(_obj) {
    setShowPopup(t_cate, t_cid);
}

function onClickCloseBtn(_obj) {
    setHidePopup();
}


function setPage(_code) {
    console.log('index setPage', _code);
    switch (_code) {
        case "00":
            break;
        case "10":
            break;
    }
}

function setHide(_hide) {
    m_clickable = true;
    $(_hide).hide();
    if ($(_hide + " .cup_img").length > 0) {
        $(_hide + " .cup_img").css("opacity", "1");
    }
}

function setInitFsCommand() {
    if (window.chrome.webview) {
        window.chrome.webview.addEventListener('message', (arg) => {
            console.log(arg.data);
            setCommand(arg.data);
        });
    }
}

function setCommand(_str) {
    let t_list = _str.split("|");
    let cmd = t_list[0];
    let arg = t_list[1];
    let t_str = "";
    console.log("setCommand", _str);
    if (cmd == "RECV") {
        $(".recv_txt").html(arg);
    }
}


