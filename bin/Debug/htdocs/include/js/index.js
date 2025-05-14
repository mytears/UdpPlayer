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
let m_cate_code = "0";
let m_mode = "0";
let m_cart_list = [];
let m_item_list = ["아이스 아메리카노", "아메리카노", "오렌지 주스", "포도 주스", "생수"];
let m_item_img_list = ["images/img_ice_coffee.png", "images/img_hot_coffee.png", "images/img_orange_jucie.png", "images/img_grape_jucie.png", "images/img_water.png"];
let m_curr_wait = 100;
let m_reset_timer;
let m_curr_playing = null;

let m_top_menu_num = 0;

function setInit() {

    $(".send_btn").on("touchstart mousedown", function (e) {
        e.preventDefault();
        //onClickUdpSendBtn(this);
    });

    $(".next_btn").on("touchstart mousedown", function (e) {
        e.preventDefault();
        //onClickNextBtn(this);
    });

    $(".top_menu").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickTopMenu(this);
    });

    $(".btn_mode").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickBtnMode(this);
    });

    $(".btn_voice").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickBtnVoice(this);
    });

    $(".menuItem").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickItem(this);
    });

    $(".btn_plus").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickItemPlus(this);
    });

    $(".btn_minus").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickItemMinus(this);
    });

    $(".btn_del").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickItemDel(this);
    });

    $(".btn_order").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickOrder(this);
    });

    $(".btn_prev").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickPrev(this);
    });

    $(".home_btn").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickHomeBtn(this);
    });

    $(".close_btn").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickCloseBtn(this);
    });

    $(".btn_pay").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickPayBtn(this);
    });

    $(".btn_cancel").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickCancelBtn(this);
    });

    $(".popupWin1 .btn_confirm").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickConfirmBtn(this);
    });

    $(".popupWin2 .btn_confirm").on("touchstart mousedown", function (e) {
        e.preventDefault();
        onClickFinalConfirmBtn(this);
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
    m_top_menu_num = 0;
    $(".popup").hide();
    $(".sub_page").hide();
    $(".main_page").show();
    $(".sub_order").hide();
    $(".main_order").show();
    m_cart_list = [];
    setCartSort();
    setBigToggle(false);
    setLowToggle(false);
    setDarkToggle(false);
    setVoiceToggle(false);
    onClickTopMenu($('.top_menu').eq(0));
}

function onClickTopMenu(_obj) {
    $(".top_menu").removeClass("active");
    let t_code = $(_obj).attr("code");
    m_cate_code = t_code;
    $(_obj).addClass("active");
    //setPage(m_cate_code);
}

function onClickBtnMode(_obj) {
    let t_code = $(_obj).attr("code");
    m_mode = t_code;
    switch (m_mode) {
        case "0":
            if ($(_obj).hasClass("active") == true) {
                setDarkToggle(false);
            } else {
                setDarkToggle(true);
            }
            break;
        case "1":
            if ($(_obj).hasClass("active") == true) {
                setLowToggle(false);
            } else {
                setLowToggle(true);
            }
            break;
        case "2":
            if ($(_obj).hasClass("active") == true) {
                setBigToggle(false);
            } else {
                setBigToggle(true);
            }
            break;
    }
}

function setDarkToggle(_bool) {
    if (_bool == false) {
        $(".btn_dark").removeClass("active");
        $("main").removeClass("mode_dark");
    } else {
        $(".btn_dark").addClass("active");
        $("main").addClass("mode_dark");
    }
}

function setLowToggle(_bool) {
    if (_bool == false) {
        $(".btn_low").removeClass("active");
        $("main").removeClass("mode_low");
    } else {
        $(".btn_low").addClass("active");
        $("main").addClass("mode_low");
    }
}

function setBigToggle(_bool) {
    if (_bool == false) {
        $(".btn_big").removeClass("active");


        $(".txt_big").each(function () {
            let original_size = $(this).data("original-size");
            if (original_size) {
                $(this).css("font-size", original_size);
            }
        });

    } else {
        $(".btn_big").addClass("active");

        $(".txt_big").each(function () {
            let $this = $(this);
            let current_size = parseFloat($this.css("font-size"));

            // 최초 1회만 저장
            if (!$this.data("original-size")) {
                $this.data("original-size", $this.css("font-size"));
            }

            // 30% 증가
            let bigger_size = current_size * 1.3;
            $this.css("font-size", bigger_size + "px");
        });
    }
}



function onClickBtnVoice(_obj) {
    if ($(_obj).hasClass("active") == true) {
        setVoiceToggle(false);
        //음성 끄기
    } else {
        setVoiceToggle(true);
        //음성 켜기
    }
}

function setVoiceToggle(_bool) {
    if (_bool == false) {
        $(".btn_voice").removeClass("active");
        //음성 끄기
        setSoundPlay("voice/voice_common_08.wav");
    } else {
        $(".btn_voice").addClass("active");
        //음성 켜기
        setSoundPlay("voice/voice_common_07.wav");
    }
}

function onClickItem(_obj) {
    if (m_cart_list.length >= 4) {
        Swal.fire({
            icon: 'error',
            title: '최대 4개까지만 주문 가능합니다.',
            heightAuto: false,
            customClass: {
                popup: 'alert',
            },
        });
        return;
    }
    let t_code = $(_obj).attr("code");
    m_cart_list.push(t_code);
    setCartSort();
    let offset = $(_obj).offset();
    let width = $(_obj).outerWidth();
    let height = $(_obj).outerHeight();
    let $clone = $(_obj).clone().appendTo('body');
    $clone.css({
        position: 'absolute',
        top: offset.top,
        left: offset.left,
        width: width,
        height: height,
        margin: 0,
        zIndex: 1000,
        pointerEvents: 'none'
    });
    let target_top = 2650;
    let target_left = 100;
    if ($(".btn_low").hasClass("active") == true) {
        target_top = 3000;
    }
    // GSAP 애니메이션
    gsap.to($clone, {
        duration: 1,
        top: target_top,
        left: target_left,
        opacity: 0,
        scale: 0.5,
        ease: "power2.out",
        onComplete: function () {
            $clone.remove();
        }
    });
}

function onClickItemPlus(_obj) {
    let $box = $(_obj).closest('.cart_box');
    let item = $box.attr('item');
    if (m_cart_list.length >= 4) {
        Swal.fire({
            icon: 'error',
            title: '최대 4개까지만 주문 가능합니다.',
            heightAuto: false,
            customClass: {
                popup: 'alert',
            },
        });
        return;
    }
    m_cart_list.push(item); // m_cart_list에 추가
    setCartSort();
}

function onClickItemMinus(_obj) {
    let $box = $(_obj).closest('.cart_box');
    let item = $box.attr('item');

    if (m_cart_list.length <= 0) {
        return;
    }
    //console.log(item);
    let index = m_cart_list.lastIndexOf(item);
    if (index !== -1) {
        m_cart_list.splice(index, 1); // 하나만 제거
        setCartSort();
    }
}

function onClickItemDel(_obj) {
    let $box = $(_obj).closest('.cart_box');
    if ($box.length == 0) {
        $box = $(_obj).closest('.order_box');
    }
    let item = $box.attr('item');

    if (m_cart_list.length <= 0) {
        return;
    }
    // m_cart_list에서 해당 item 값 전부 제거
    m_cart_list = m_cart_list.filter(function (val) {
        return val !== item;
    });
    setCartSort();
}

function setCartSort() {
    if (m_cart_list.length == 0) {
        $(".btn_order").addClass("disabled");
        $(".btn_pay").addClass("disabled");
    } else {
        $(".btn_order").removeClass("disabled");
        $(".btn_pay").removeClass("disabled");
    }
    $(".txt_total").html(m_cart_list.length);

    console.log("m_cart_list", m_cart_list);


    // 1. 각 코드별로 개수 집계
    let item_counts = {};

    m_cart_list.forEach(function (code_str) {
        let code = parseInt(code_str);
        if (item_counts[code]) {
            item_counts[code]++;
        } else {
            item_counts[code] = 1;
        }
    });


    // 2. cart_box 초기화
    $('.cart_box').hide();
    $('.order_box').hide();

    // 3. item_counts에서 등장 순서대로 cart_box 채우기
    let cart_index = 0;
    for (let i = 0; i < m_cart_list.length; i++) {
        let code = parseInt(m_cart_list[i]);

        // 이미 처리한 상품이면 skip
        if (item_counts[code] === false) continue;

        // 해당 cart_box 선택
        let $cart_box = $('.cart_box').eq(cart_index);

        $cart_box.attr("item", m_cart_list[i]);
        $cart_box.find('.txt_name').text(m_item_list[code]);
        $cart_box.find('.txt_count').text(item_counts[code]);
        $cart_box.show();

        // 해당 cart_box 선택
        let $order_box = $('.order_box').eq(cart_index);

        $order_box.attr("item", m_cart_list[i]);
        $order_box.find('.txt_name').text(m_item_list[code]);
        $order_box.find('.txt_count').text(item_counts[code]);
        $order_box.find('.img_zone img').attr("src", m_item_img_list[m_cart_list[i]]);
        $order_box.show();

        // 이 상품은 이미 처리했으니 다시 넣지 않게 false 처리
        item_counts[code] = false;
        cart_index++;
    }
}

function onClickOrder(_obj) {
    if ($(_obj).css("opacity") == "1") {
        $(".sub_page").show();
        $(".sub_order").show();
        $(".main_page").hide();
        $(".main_order").hide();
    }
}

function onClickPrev(_obj) {
    if ($(_obj).css("opacity") == "1") {
        $(".sub_page").hide();
        $(".sub_order").hide();
        $(".main_page").show();
        $(".main_order").show();
    }
}

function setShowPopup(_num) {
    console.log("setShowPopup", _num);
    $(".popupWin1").hide();
    $(".popupWin2").hide();
    switch (_num) {
        case "0":
            $(".popupWin1").show();
            break;
        case "1":
            $(".popupWin2").show();
            m_curr_wait += 1;
            $(".popupWin2 .num").html(m_curr_wait.toString());
            m_reset_timer = setTimeout(setMainReset, 3000);
            break;
    }
    $(".popup").show();
}

function setHidePopup() {
    $(".popup").hide();
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

function onClickPrevBtn(_obj) {}

function onClickNextBtn(_obj) {}

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
    setMainReset();
}

function onClickPayBtn(_obj) {
    setShowPopup("0");
}

function onClickCancelBtn(_obj) {
    setHidePopup();
}

function onClickConfirmBtn(_obj) {
    setShowPopup("1");
}

function onClickFinalConfirmBtn(_obj) {
    clearTimeout(m_reset_timer);
    setMainReset();
}

function onClickPopupBtn(_obj) {
    //setShowPopup(t_cate, t_cid);
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
}

function setSoundPlay(_sound) {
    console.log("setSoundPlay", _sound);
    if (m_curr_playing) {
        m_curr_playing.pause();
        m_curr_playing.currentTime = 0;
        m_curr_playing.src = _sound;
    } else {
        m_curr_playing = new Audio(_sound);
    }
    m_curr_playing.play();
}






function onRecvKeypad(_cmd) {
    console.log(_cmd);

    if (_cmd == "Left" || _cmd == "Right" || _cmd == "Up" || _cmd == "Down") {
        setArrowKeypad(_cmd);
    } else if (_cmd == "*") {

    } else if (_cmd == "#") {

    } else if (_cmd == "h") {
        setMainReset();
    } else if (_cmd == "Back") {

    } else if (_cmd == "Return") {
        setSelectEvent();
    } else {}
}

function setArrowKeypad(_cmd) {
    console.log("setArrowKeypad", _cmd);
    if (_cmd == "Left") {
        if (m_top_menu_num > 1) {
            m_top_menu_num -= 1;
        }
        setMenuOutline(m_top_menu_num);
    } else if (_cmd == "Right") {        
        if (m_top_menu_num < 5) {
            m_top_menu_num += 1;
        }
        setMenuOutline(m_top_menu_num);
    } else if (_cmd == "Up") {
        console.log("DD");
    } else if (_cmd == "Down") {
        console.log("DD");
    }
}

function setMenuOutline(_num) {
    console.log("setMenuOutline", _num);
    $('.menuType li').removeClass('btn_outline');
    $('.menuType li:nth-child(' + _num + ')').addClass('btn_outline');
    setSoundPlay("voice/voice_top_0" + (parseInt(_num)) + ".wav");
}

function setSelectEvent() {
    if (m_curr_document == null) {
        if (m_chk_page_num > 0) {
            const t_btn = $(`.middle_nav li[code="${m_chk_page_num}"]`);
            test_startTime = Date.now();
            onClickMainMenu(t_btn);
        }
    } else {
        m_curr_document.setSelectEvent();
    }
}


function onClickBtnBack(_obj) {
    if ($("#id_popup_pdf").css("display") != "none" || $("#id_popup_video").css("display") != "none") {
        onClickBtnPopupClose();
    } else {
        if (m_curr_document != null) {
            m_curr_document.onClickBtnBack();
        }
    }
}

function onClickBtnHome(_obj) {
    setMainReset();
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
